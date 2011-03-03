using System;
using System.Collections.Generic;
using System.Linq;
using LinqToSqlXml;
using System.Diagnostics;

namespace ProjectionSample
{
    public class Projection
    {
        public decimal OrderTotal { get; set; }
        public Guid CustomerId { get; set; }
        public IEnumerable<ProjectionD> OrderDetails { get; set; }
    }
    
    public class ProjectionD
    {
        public decimal LineTotal { get; set; }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            var ctx = new DocumentContext("main");
            ctx.EnsureDatabaseExists();


            var query = (from order in ctx.GetCollection<Order>().AsQueryable().OfType<Order>()
                                            where order.OrderTotal > 0
                                            where order.ShippingDate != null
                                            where order.ShippingAddress.Line1 != "aa"
                                            //select order
                                            select new Projection
                                                       {
                                                           OrderTotal = order.OrderTotal,
                                                           CustomerId = order.CustomerId,
                                                           OrderDetails =
                                                               order.OrderDetails.Select(
                                                                   d =>
                                                                   new ProjectionD()
                                                                       {
                                                                           LineTotal = d.ItemPrice * d.Quantity
                                                                       }),
                                                       }
                                                       ).Take(100);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            var result = query.ToList();
            sw.Stop();
            

            foreach (var order in result)
            {
                Console.WriteLine("{0} {1}", order.OrderTotal, order.OrderDetails.Count());
            }

            Console.WriteLine(sw.Elapsed);
            Console.ReadLine();
            return;

            for (int i = 0; i < 1000; i++)
            {
                Console.WriteLine(i);
                var someCompany = new Customer
                                  {
                                      Address = new Address
                                                    {
                                                        City = "Stora mellösa",
                                                        Line1 = "Linfrövägen " + i,
                                                        State = "T",
                                                        ZipCode = "71572"
                                                    },
                                      Name = "Precio" + i,

                                  };

                ctx.GetCollection<Customer>().Add(someCompany);

                var someOrder = new Order
                                       {
                                           CustomerId = Guid.NewGuid(),
                                           OrderDate = DateTime.Now,
                                           ShippingDate = DateTime.Now,
                                           OrderDetails = new List<OrderDetail>
                                                              {
                                                                  new OrderDetail
                                                                      {
                                                                          ItemPrice = 123,
                                                                          ProductNo = "banan",
                                                                          Quantity = 432
                                                                      },
                                                                      new OrderDetail
                                                                      {
                                                                          ItemPrice = 123,
                                                                          ProductNo = "äpple",
                                                                          Quantity = 432
                                                                      },
                                                                      new OrderDetail
                                                                      {
                                                                          ItemPrice = 123,
                                                                          ProductNo = "gurka",
                                                                          Quantity = 432
                                                                      },
                                                              },
                                           ShippingAddress = new Address
                                                                 {
                                                                     City = "gdfgdf",
                                                                     Line1 = "dfgdgdfgd",
                                                                     ZipCode = "gdfgdfgd"
                                                                 },
                                           Status = OrderStatus.Shipped,
                                       };

                ctx.GetCollection<Order>().Add(someOrder);
                //var result = DocumentSerializer.Serialize(specialOrder);
                //Console.WriteLine(result.ToString());
                //ctx.GetCollection<Order>().Add(specialOrder);
                //ctx.SaveChanges();
                //var des = DocumentDeserializer.Deserialize(result);






                //    var address = new Address()
                //                      {
                //                          City = "Örebro",
                //                          Line1 = "blabla",
                //                          ZipCode = "" + i ,
                //                      };

                //    ctx.GetCollection<Address>().Add(address);
                //}
                
            }
            ctx.SaveChanges();
            Console.ReadLine();
        }
    }

    public interface IAmDocument
    {
    }

    public class SpecialOrder : Order, IAmDocument
    {
    }

    public class Customer
    {
        [DocumentId]
        public Guid Id { get; private set; }

        public Customer()
        {
            this.Id = Guid.NewGuid();
        }

        public string Name { get; set; }
        public Address Address { get; set; }

        public Order NewOrder()
        {
            return new Order
            {
                CustomerId = this.Id,
            };
        }
    }

    public class Order
    {
        [DocumentId]
        public Guid Id { get; private set; }

        public Order()
        {
            this.Id = Guid.NewGuid();
        }

        public DateTime OrderDate { get; set; }
        public DateTime? ShippingDate { get; set; }
        public Guid CustomerId { get; set; }
        public Address ShippingAddress { get; set; }
        public ICollection<OrderDetail> OrderDetails { get; set; }

        public decimal OrderTotal
        {
            get { return OrderDetails.Sum(d => d.Quantity * d.ItemPrice); }
        }

        public OrderStatus Status { get; set; }
    }

    public enum OrderStatus
    {
        PreOrder,
        Confirmed,
        Shipped,
        Cancelled,
    }

    public class OrderDetail
    {
        public decimal Quantity { get; set; }
        public decimal ItemPrice { get; set; }
        public string ProductNo { get; set; }
    }

    public class Address
    {
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public string Line3 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }
    }
}