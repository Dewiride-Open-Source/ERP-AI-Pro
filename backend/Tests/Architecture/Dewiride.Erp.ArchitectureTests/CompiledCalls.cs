using System.Reflection;
using System.Reflection.Emit;

namespace Dewiride.Erp.ArchitectureTests;

// Reads the instructions of every method an assembly declares for the methods they call or take as a delegate. ArchUnit records
// no dependency of the method the compiler makes from top-level statements, which is where a host composes itself, so a rule
// about who calls a method reads the instructions themselves.
internal static class CompiledCalls
{
    private const BindingFlags DeclaredMembers = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private const byte TwoByteOpCodePrefix = 0xFE;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    public static bool AnyTo(MethodInfo target, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly.GetTypes()
            .SelectMany(type => type.GetMethods(DeclaredMembers).Cast<MethodBase>().Concat(type.GetConstructors(DeclaredMembers)))
            .Any(method => CalledBy(method).Any(called => called.Module == target.Module && called.MetadataToken == target.MetadataToken));
    }

    private static IEnumerable<MethodBase> CalledBy(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var typeArguments = method.DeclaringType is { IsGenericType: true } declaringType ? declaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        for (var position = 0; position < il.Length;)
        {
            var code = OpCodesByValue[il[position] == TwoByteOpCodePrefix ? unchecked((short)((TwoByteOpCodePrefix << 8) | il[position + 1])) : il[position]];
            position += code.Size;
            if (code.OperandType == OperandType.InlineMethod && method.Module.ResolveMethod(BitConverter.ToInt32(il, position), typeArguments, methodArguments) is { } called)
            {
                yield return called;
            }

            position += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
                _ => 4,
            };
        }
    }
}
