using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace AutoAllegro.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class User : IdentityUser
    {
        public virtual ICollection<Auction> Auctions { get; set; } = new List<Auction>();
        public string AllegroKey { get; set; }
        public string AllegroUserName { get; set; }
        public string AllegroHashedPass { get; set; }
    }
}
