namespace Oms.Application.Mappings;

using AutoMapper;
using Oms.Domain.Aggregates.Order;
using Oms.Domain.Aggregates.Customer;
using Oms.Domain.Aggregates.Payment;
using Oms.Domain.ValueObjects;
using Oms.Application.DTOs;

/// <summary>AutoMapper profile for domain-to-DTO mappings</summary>
public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        // Order mappings
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.OrderStatus, opt => opt.MapFrom(s => s.CurrentStatus.ToString()))
            .ForMember(d => d.Items, opt => opt.MapFrom(s => s.Items))
            .ForMember(d => d.Customer, opt => opt.MapFrom(s => s.OrderCustomer))
            .ForMember(d => d.Payment, opt => opt.MapFrom(s => s.OrderPayment))
            .ForMember(d => d.RiskAssessment, opt => opt.MapFrom(s => s.RiskAssessment))
            .ForMember(d => d.EnrichedData, opt => opt.MapFrom(s => s.EnrichedData));

        CreateMap<OrderItem, OrderItemDto>()
            .ForMember(d => d.LineTotal, opt => opt.MapFrom(s => s.GetLineTotal()));

        // Customer mappings
        CreateMap<Customer, CustomerDto>()
            .ForMember(d => d.Segment, opt => opt.MapFrom(s => s.Segment.ToString()));

        CreateMap<Address, AddressDto>().ReverseMap();

        // Payment mappings
        CreateMap<Payment, PaymentDto>()
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()));

        CreateMap<PaymentTransaction, PaymentTransactionDto>()
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()));

        // Risk mappings
        CreateMap<RiskData, RiskDataDto>()
            .ForMember(d => d.Level, opt => opt.MapFrom(s => s.Level.ToString()));

        CreateMap<RiskIndicator, RiskIndicatorDto>();

        // Enrichment mappings
        CreateMap<EnrichedOrder, EnrichedOrderDto>();
        CreateMap<EnrichedOrderItem, EnrichedOrderItemDto>();
    }
}

/// <summary>AutoMapper profile for DTO-to-Domain mappings (for creation)</summary>
public class CreateOrderMappingProfile : Profile
{
    public CreateOrderMappingProfile()
    {
        // Create order from DTO
        CreateMap<CreateOrderDto, (Order order, Customer customer)>()
            .ConstructUsing((dto, ctx) =>
            {
                var customer = Customer.Create(
                    dto.CustomerEmail,
                    dto.CustomerName,
                    dto.CustomerPhone,
                    ctx.Mapper.Map<Address>(dto.ShippingAddress),
                    ctx.Mapper.Map<Address>(dto.BillingAddress)
                );

                var order = Order.Create(
                    dto.CustomerId,
                    dto.OrderNumber,
                    dto.TotalAmount,
                    customer,
                    DateTime.UtcNow.AddHours(24) // 24-hour SLA
                );

                return (order, customer);
            });

        CreateMap<CreateOrderItemDto, OrderItem>()
            .ConstructUsing((dto, ctx) =>
                OrderItem.Create(
                    dto.ProductCode,
                    dto.ProductName,
                    dto.UnitPrice,
                    dto.Quantity
                )
            );

        CreateMap<AddressDto, Address>();
    }
}

/// <summary>Extension methods for mapping operations</summary>
public static class MappingExtensions
{
    /// <summary>Map order item DTOs to domain entities</summary>
    public static List<OrderItem> ToOrderItems(this List<CreateOrderItemDto> dtos)
    {
        return dtos.Select(dto => OrderItem.Create(
            dto.ProductCode,
            dto.ProductName,
            dto.UnitPrice,
            dto.Quantity
        )).ToList();
    }

    /// <summary>Map to address domain entity</summary>
    public static Address ToAddress(this AddressDto dto)
    {
        return new Address
        {
            Street = dto.Street,
            City = dto.City,
            State = dto.State,
            ZipCode = dto.ZipCode,
            Country = dto.Country
        };
    }
}
