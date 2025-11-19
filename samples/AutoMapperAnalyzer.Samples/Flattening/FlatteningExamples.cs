namespace AutoMapperAnalyzer.Samples.Flattening;

public class FlatteningExamples
{
    public class Order
    {
        public Customer Customer { get; set; }
        public decimal Total { get; set; }
    }

    public class Customer
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class OrderDto
    {
        public string CustomerName { get; set; } // Should be mapped from Customer.Name
        public decimal Total { get; set; }
    }

    public class MappingProfile : AutoMapper.Profile
    {
        public MappingProfile()
        {
            // Should map Customer.Name -> CustomerName automatically by flattening convention
            CreateMap<Order, OrderDto>();
        }
    }
}

