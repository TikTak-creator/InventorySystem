namespace Inventory.Storage;

public sealed class MissingEntityException(string message) : InvalidOperationException(message);
public sealed class ConflictException(string message) : InvalidOperationException(message);
