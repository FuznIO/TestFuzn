namespace Fuzn.TestFuzn.Tests.InputData;

public class ComplexUser
{
    public string Name { get; set; } = null!;
    public string Username { get; set; } = null!;
    public Address[] Address { get; set; } = null!;
    public List<PhoneNumber> PhoneNumbers { get; set; } = null!;
    public int Counter { get; set; }
}
