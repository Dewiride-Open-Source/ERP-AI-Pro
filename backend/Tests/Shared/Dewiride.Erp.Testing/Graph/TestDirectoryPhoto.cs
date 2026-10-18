namespace Dewiride.Erp.Testing.Graph;

// Graph serves a photo it keeps in sizes at /photos/240x240 too, and one it keeps at a size of its own only at /photo.
public sealed record TestDirectoryPhoto(byte[] Content, string ContentType, bool Sized = true);
