using Dewiride.Erp.BuildingBlocks.Kernel.Domain;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Domain;

public sealed class EntityEqualityTests
{
    private static readonly Guid SharedId = new("0199a1b2-0000-7000-8000-000000000001");

    private static readonly DateTimeOffset PlacedAt = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Equals_SameTypeAndId_IsEqualAndSharesTheHashCode()
    {
        var first = new Customer(SharedId);
        var second = new Customer(SharedId);

        Assert.Equal(first, second);
        Assert.True(first.Equals((object)second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_SameTypeWithDifferentIds_IsNotEqual()
    {
        var first = new Customer(Guid.CreateVersion7());
        var second = new Customer(Guid.CreateVersion7());

        Assert.NotEqual(first, second);
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Equals_DifferentRuntimeTypesWithTheSameId_IsNotEqual()
    {
        var customer = new Customer(SharedId);
        var product = new Product(SharedId);

        Assert.False(customer.Equals(product));
        Assert.False(product.Equals(customer));
    }

    [Fact]
    public void Equals_NullOrUnrelatedObject_IsFalse()
    {
        var customer = new Customer(SharedId);

        Assert.False(customer.Equals(null));
        Assert.False(customer.Equals(SharedId));
    }

    [Fact]
    public void Equals_SameInstance_IsTrue()
    {
        var customer = new Customer(SharedId);

        Assert.True(customer.Equals(customer));
    }

    [Fact]
    public void Raise_DomainEvents_AppearInTheOrderRaised()
    {
        var order = new Order(SharedId);

        order.Place(PlacedAt);
        order.Place(PlacedAt.AddMinutes(1));

        Assert.Equal([new OrderPlaced(PlacedAt), new OrderPlaced(PlacedAt.AddMinutes(1))], order.DomainEvents);
    }

    [Fact]
    public void DomainEvents_NewAggregate_IsEmpty()
    {
        var order = new Order(SharedId);

        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_RaisedEvents_LeavesNone()
    {
        var order = new Order(SharedId);
        order.Place(PlacedAt);

        order.ClearDomainEvents();

        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void Equals_AggregateRoots_UseTheEntityIdentity()
    {
        var first = new Order(SharedId);
        var second = new Order(SharedId);
        first.Place(PlacedAt);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    private sealed class Customer(Guid id) : Entity<Guid>(id);

    private sealed class Product(Guid id) : Entity<Guid>(id);

    private sealed class Order(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Place(DateTimeOffset at) => Raise(new OrderPlaced(at));
    }

    private sealed record OrderPlaced(DateTimeOffset OccurredOn) : IDomainEvent;
}
