namespace Oms.Domain.Aggregates.Customer;

using Oms.Domain.Enums;

/// <summary>Customer entity with contact and segment information</summary>
public class Customer
{
    public Guid CustomerId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    
    public Address ShippingAddress { get; private set; } = new();
    public Address BillingAddress { get; private set; } = new();
    
    public CustomerSegment Segment { get; private set; }
    public int PreviousOrderCount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // EF Core constructor
    private Customer() { }

    public static Customer Create(
        string email,
        string name,
        string phone,
        Address shippingAddress,
        Address billingAddress)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required");
        
        return new Customer
        {
            CustomerId = Guid.NewGuid(),
            Email = email,
            Name = name,
            Phone = phone,
            ShippingAddress = shippingAddress,
            BillingAddress = billingAddress,
            Segment = CustomerSegment.New,
            PreviousOrderCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void IncrementOrderCount()
    {
        PreviousOrderCount++;
        if (PreviousOrderCount > 10 && Segment == CustomerSegment.New)
            Segment = CustomerSegment.Loyal;
    }

    public void FlagCustomer()
    {
        Segment = CustomerSegment.Flagged;
    }
}

/// <summary>Value object representing physical address</summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    
    public override bool Equals(object? obj) =>
        obj is Address other &&
        Street == other.Street &&
        City == other.City &&
        State == other.State &&
        ZipCode == other.ZipCode &&
        Country == other.Country;
    
    public override int GetHashCode() =>
        HashCode.Combine(Street, City, State, ZipCode, Country);
}

/// <summary>Repository interface for Customer persistence</summary>
public interface ICustomerRepository
{
    Task AddAsync(Customer customer);
    Task<Customer?> GetByIdAsync(Guid customerId);
    Task UpdateAsync(Customer customer);
    Task<Customer?> GetByEmailAsync(string email);
}
