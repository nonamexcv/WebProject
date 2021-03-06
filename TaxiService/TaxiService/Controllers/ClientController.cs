﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using TaxiService.Models;
using TaxiService.Models.Base;
using TaxiService.Models.Security;

namespace TaxiService.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class ClientController : ApiController
    {
        private static LocationBase CurrentLocation = null;
        private static String CarType = CarRole.Not_Specified;
        private static FilterBase filter = new FilterBase();
        public CustomPrincipal AuthUser
        {
            get
            {
                if (HttpContext.Current.User != null)
                {
                    return HttpContext.Current.User as CustomPrincipal;
                }
                else
                {
                    return null;
                }
            }
        }

        private static DataAccess DB = DataAccess.CreateDb();

        [HttpPost,Route("api/Client/AddClient")]
        [AllowAnonymous]
        public IHttpActionResult AddClient(Client data)
        {
            lock (DB)
            {
                if (!DB.DriverDb.ToList().Exists(p => p.Username == data.Username)
                        && !DB.ClientDb.ToList().Exists(p => p.Username == data.Username)
                        && !DB.DispacherDb.ToList().Exists(p => p.Username == data.Username) && !string.IsNullOrEmpty(data.Username))
                {
                    data.RideList = new List<RideBase>();
                    // data.ID = DB.ClientDb.ToList().Count() + 1;
                    data.Role = UserRole.ClientRole;
                    DB.UserDb.Add(new LoginBase()
                    {
                        Username = data.Username,
                        Password = data.Password,
                        Role = data.Role
                    });
                    DB.ClientDb.Add(data);
                    DB.SaveChanges();
                    return Ok(data);
                }
                else
                {
                    return BadRequest();
                }

            }
        }

        [HttpPost,Route("api/Client/Update")]
        public IHttpActionResult Update(Client data)
        {
            lock (DB)
            {
                int id = DB.ClientDb.ToList().IndexOf(DB.ClientDb.ToList().Find(p => p.Username == data.Username));
                DB.ClientDb.ToList()[id].Password = data.Password;
                DB.ClientDb.ToList()[id].ContactPhone = data.ContactPhone;
                DB.ClientDb.ToList()[id].Email = data.Email;
                DB.ClientDb.ToList()[id].Firstname = data.Firstname;
                DB.ClientDb.ToList()[id].Lastname = data.Lastname;
                DB.ClientDb.ToList()[id].Gender = data.Gender;
                DB.ClientDb.ToList()[id].JMBG = data.JMBG;
                DB.SaveChanges();
                return Ok();
            }
        }
        
        [HttpPost,Route("api/Client/LogOff")]
        public IHttpActionResult LogOff(LoginBase data)
        {
            lock (DB)
            {
                System.Web.Security.FormsAuthentication.SignOut();
                return Ok();
            }
        }

        [HttpPost,Route("api/Client/AddLocation")]
        public IHttpActionResult AddLocation(LocationBase data)
        {
            CurrentLocation = data;
            return Ok();
        }
        [HttpPost,Route("api/Client/AddCarType/{id:int}")]
        public IHttpActionResult AddCarType(int id)
        {
            if(id == 0)
            {
                CarType = CarRole.Not_Specified;
            }
            else if (id == 1)
            {
                CarType = CarRole.Sedan;
            }
            else
            {
                CarType = CarRole.Van;
            }
            return Ok();
        }

        [HttpPost,Route("api/Client/OrderRide")]
        public IHttpActionResult OrderRide()
        {
            lock (DB)
            {
                try
                {
                    Client LoggedClient = DB.ClientDb.ToList().Find(p => p.Username == AuthUser.Username);

                    LoggedClient.RideList = new List<RideBase>
            {
                new RideBase()
                {
                    CarType = CarType,
                    Status = RideStatus.Created,
                    RideClient = LoggedClient.ID,
                    CommentID = 0,
                    Location = new LocationBase(){
                        Place = CurrentLocation.Place,
                        StreetName = CurrentLocation.StreetName,
                        XCoordinate = CurrentLocation.XCoordinate,
                        YCoordinate = CurrentLocation.YCoordinate,
                        ZipCode = CurrentLocation.ZipCode
                    },
                    Destination = new LocationBase(),
                    AdminID = 0,
                    RidePrice = 0,
                    RiderOrderDate = DateTime.Now.ToString(),
                    TaxiRiderID = 0
                }
            };
                    DB.ClientDb.ToList()[DB.ClientDb.ToList().IndexOf(DB.ClientDb.ToList().Find(p => p.Username == AuthUser.Username))] = LoggedClient;

                    DB.SaveChanges();

                    return Ok();
                }
                catch
                {
                    return NotFound();
                }
            }

        }

        [HttpGet,Route("api/Client/getRides")]
        public IHttpActionResult getRides()
        {
            lock (DB)
            {
                List<RideBase> rides = DB.RideDb.ToList();
                Client user = DB.ClientDb.ToList().Find(p => p.Username == AuthUser.Username);
                if (rides.Count != 0)
                {
                    rides.ForEach(ride =>
                    {
                        ride.Location = DB.LocationDb.ToList().Find(p => p.ID == ride.Location.ID);
                        ride.Destination = DB.LocationDb.ToList().Find(p => p.ID == ride.Destination.ID);
                    });
                    List<RideBase> sortedList = rides.Where(x=> x.RideClient == user.ID).ToList();
                    List<CommentBase> comments = DB.CommentDb.ToList();

                    //SearchByDate
                    sortedList = sortedList.Where(x => DateTime.Parse(x.RiderOrderDate) >= DateTime.Parse(filter.FromDate.ToShortDateString()) && DateTime.Parse(x.RiderOrderDate) <= DateTime.Parse(filter.ToDate.ToShortDateString())).ToList();

                    //SearchByPrice
                    sortedList = sortedList.Where(x => x.RidePrice >= filter.FromPrice && x.RidePrice <= filter.ToPrice).ToList();
                    //SearchByGrade
                    if(filter.FromGrade > 0)
                    {
                        sortedList.RemoveAll(x => x.CommentID == 0);
                        List<RideBase> newSorted = new List<RideBase>();
                        sortedList.ForEach(x =>
                        {
                            if (DB.CommentDb.ToList().Find(y => y.RideID == x.CommentID).Stars >= filter.FromGrade && DB.CommentDb.ToList().Find(y => y.RideID == x.CommentID).Stars <= filter.ToGrade)
                            {
                                newSorted.Add(x);
                            }
                        });
                        sortedList = newSorted;
                    }

                        //filter options
                        if (!string.IsNullOrEmpty(filter.FilterStatus) && filter.FilterStatus !="None")
                    {
                        sortedList = sortedList.Where(x => string.Equals(filter.FilterStatus, x.Status)).ToList();
                    }
                    //Sort options
                    if (filter.SortDate == "Newest")
                    {
                        sortedList = sortedList.OrderByDescending(x => x.RiderOrderDate).ThenBy(x => x.RiderOrderDate).ToList();
                    }
                    if (filter.SortDate == "Oldest")
                    {
                        sortedList = sortedList.OrderBy(x => x.RiderOrderDate).ThenBy(x => x.RiderOrderDate).ToList();
                    }
                    if(filter.SortGrade == "Highest")
                    {
                        List<RideBase> order = new List<RideBase>();
                        sortedList.ForEach(x=> order.Add(x));
                        order.RemoveAll(x => x.CommentID == 0);
                        order = order.OrderByDescending(x => DB.CommentDb.ToList().Find(y => y.RideID == x.CommentID).Stars).ToList();
                        List<RideBase> newList = new List<RideBase>();
                        order.ForEach(o =>
                        {
                            newList.Add(o);
                        });
                        sortedList.ForEach(item =>
                        {
                            if(!newList.Exists(p=> p.ID == item.ID))
                            {
                                newList.Add(item);
                            }
                        });
                        sortedList = newList;
                    }
                    if (filter.SortGrade == "Lowest")
                    {
                        List<RideBase> order = new List<RideBase>();
                        sortedList.ForEach(x=>order.Add(x));
                        order.RemoveAll(x => x.CommentID == 0);
                        order = order.OrderBy(x => DB.CommentDb.ToList().Find(y => y.RideID == x.CommentID).Stars).ToList();
                        List<RideBase> newList = new List<RideBase>();
                        order.ForEach(o =>
                        {
                            newList.Add(o);
                        });
                        sortedList.ForEach(item =>
                        {
                            if (!newList.Exists(p => p.ID == item.ID))
                            {
                                newList.Add(item);
                            }
                        });
                        sortedList = newList;
                    }
                    //end of sort
                    //
                    return Ok(sortedList);
                }
            }
                return NotFound();
        }

        [HttpGet,Route("api/Client/getComment{id:int}")]
        public IHttpActionResult getComment(int id)
        {
            lock (DB)
            {
                CommentBase comment = DB.CommentDb.ToList().Find(p => p.RideID == id);
                return Ok(comment ?? new CommentBase());
            }
        }

        [HttpPost,Route("api/Client/addComment")]
        public IHttpActionResult addComment(CommentBase data)
        {
            lock (DB)
            {
                RideBase ride = DB.RideDb.ToList().Find(p => p.ID == data.ID);
                List<CommentBase> comment = DB.CommentDb.ToList();
                if (!comment.Exists(p => p.RideID == data.ID))
                {
                    if (ride.Status == RideStatus.Canceled)
                    {
                        data.Stars = 0;
                    }
                    DB.CommentDb.Add(new CommentBase()
                    {
                        CommentDate = DateTime.Now.ToString(),
                        Stars = data.Stars,
                        Summary = data.Summary,
                        OriginalPoster = AuthUser.Username,
                        RideID = ride.ID
                    });
                    ride.CommentID = data.ID;

                    DB.SaveChanges();

                    return Ok(DB.CommentDb.ToList().Find(p => p.RideID == data.ID));
                }
                return NotFound();
            }
        }
        [HttpGet, Route("api/Client/CheckIfBanned")]
        public IHttpActionResult CheckIfBanned()
        {
            lock (DB)
            {
                if (DB.BlockDb.ToList().Exists(x => x.Username == AuthUser.Username))
                {
                    return Ok(true);
                }
                else
                {
                    return Ok(false);
                }
            }
        }
        [HttpPost,Route("api/Client/CancelRide{id:int}")]
        public IHttpActionResult CancelRide(int id)
        {
            lock (DB)
            {
                DB.RideDb.ToList().Find(x => x.ID == id).Status = RideStatus.Canceled;
                return Ok();
            }
        }

        [HttpPost,Route("api/Client/ApplyFilter")]
        public IHttpActionResult ApplyFilter(FilterBase filterBase)
        {
            filter = filterBase;
            if(filter.ToDate.ToString() == "01-Jan-01 12:00:00 AM")
            {
                filter.ToDate = DateTime.Now;
            }
            if(filter.ToPrice == 0)
            {
                filter.ToPrice = 90000;
            }
            if(filter.ToGrade == 0)
            {
                filter.ToGrade = 5;
            }
            filter.ToDate = filterBase.ToDate.AddDays(1);
            return Ok();
        }
    }
}
