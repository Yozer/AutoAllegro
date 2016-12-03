using System;
using System.Collections.Generic;

namespace AutoAllegro.Models
{
    public class Buyer
    {
        public int Id { get; set; }
        public long AllegroUserId { get; set; }

        public string UserLogin { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PostCode { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Phone2 { get; set; }
        public string UserBirthDate { get; set; }

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }

}
