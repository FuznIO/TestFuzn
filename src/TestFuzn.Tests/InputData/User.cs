namespace Fuzn.TestFuzn.Tests.InputData;

public class User(string name)
{
    public string Name { get; set; } = name;
    public int Counter;

    public override string ToString()
    {
        return "User: " + (Name ?? "");
    }
}
