namespace Fuzn.TestFuzn.Tests.InputData;

public class SimpleUser
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Street { get; set; } = null!;
    public string City { get; set; } = null!;
    public string ZipCode { get; set; } = null!;
    public int Age { get; set; }
    public decimal? Income { get; set; } = null!;
    public bool IsActive { get; set; }
}
