namespace TestFusion.Tests.InputData;

public class ComplexUser
{
    public string Name { get; set; }
    public string Username { get; set; }
    public Address[] Address { get; set; }
    public List<PhoneNumber> PhoneNumbers { get; set; }
    public int Counter;
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}

public class PhoneNumber
{
    public string CountyCode { get; set; }
    public string Number { get; set; }
}
