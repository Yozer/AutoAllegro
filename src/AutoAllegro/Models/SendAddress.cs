﻿
namespace AutoAllegro.Models
{
    public class ShippingAddress
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PostCode { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string MessageToSeller { get; set; }

        public virtual Order Order { get; set; }
    }
}
