using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqToSqlXml;

namespace ProjectionSample
{

    public class Projection
    {
        public decimal OrderTotal { get; set; }
        public Guid CustomerId { get; set; }
        public ICollection<OrderDetail> Details { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var ctx = new DocumentContext("main");
            ctx.EnsureDatabaseExists();


            var query = (from order in ctx.GetCollection<Order>().AsQueryable()
                where order.OrderTotal > 100000000 
                where order.ShippingDate == null
                where order.OrderDetails.Sum(d => d.Quantity * d.ItemPrice) > 10
                select new Projection
                { 
                    OrderTotal = order.OrderDetails.Sum(d => d.ItemPrice * d.Quantity),
                    CustomerId = order.CustomerId , 
                    Details = order.OrderDetails
                })
                .Take(5);

            var result = query.ToList();

            foreach (var order in result)
            {
                Console.WriteLine("{0} {1}",order.OrderTotal,order.Details.Count);
            }
            Console.ReadLine();

            //for (int i = 6000; i < 12000; i++)
            //{
            //    Console.WriteLine(i);
            //    var acmeInc = new Customer
            //                      {
            //                          Address = new Address
            //                                        {
            //                                            City = "Stora mellösa",
            //                                            Line1 = "Linfrövägen " + i,
            //                                            State = "T",
            //                                            ZipCode = "71572"
            //                                        },
            //                          Name = "Precio" + i ,

            //                      };

            //    ctx.GetCollection<Customer>().Add(acmeInc);

            //    var specialOrder = new Order
            //                           {
            //                               CustomerId = Guid.NewGuid(),
            //                               OrderDate = DateTime.Now,
            //                               OrderDetails = new List<OrderDetail>
            //                                                  {
            //                                                      new OrderDetail
            //                                                          {
            //                                                              ItemPrice = i,
            //                                                              ProductNo = "foo" + i,
            //                                                              Quantity = i
            //                                                          },
            //                                                  },
            //                               ShippingAddress = new Address
            //                                                     {
            //                                                         City = "Örebro",
            //                                                         Line1 = "Fabriksgatan 123",
            //                                                         ZipCode = "71580"
            //                                                     },
            //                               Status = OrderStatus.PreOrder,
            //                           };

            //    ctx.GetCollection<Order>().Add(specialOrder);

            //    var address = new Address()
            //                      {
            //                          City = "Örebro",
            //                          Line1 = "blabla",
            //                          ZipCode = "" + i ,
            //                      };

            //    ctx.GetCollection<Address>().Add(address);
            //}
            //ctx.SaveChanges();
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
        public string Name { get; set; }
        public Address Address { get; set; }
    }

    public class Order
    {
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
