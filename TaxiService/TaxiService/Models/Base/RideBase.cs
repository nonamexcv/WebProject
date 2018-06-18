﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace TaxiService.Models
{
    public class RideBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }
        
        public String RiderOrderDate { get; set; }

        public LocationBase Location { get; set; }

        public CarRole CarType { get; set; }

        public int RideClient { get; set; }

        public LocationBase Destination { get; set; }

        //public int DispacherID { get; set; }
        public virtual Dispacher DispacherID { get; set; }

        //public int DriverID { get; set; }
        public virtual Driver DriverID { get; set; }

        public double RidePrice { get; set; }
        
        //public int CommentID { get; set; }
        public virtual CommentBase CommentID { get; set; }

        public RideStatus Status { get; set; }
    }
}