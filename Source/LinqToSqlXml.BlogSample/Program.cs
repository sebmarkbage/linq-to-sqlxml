using LinqToSqlXml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlogSample
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var ctx = new DocumentContext("main");

            //var blogpost = new BlogPost()
            //                   {
            //                       Body = "gdfg",
            //                       Topic = "fsdfs world",
            //                   };

            //blogpost.ReplyTo("Hej hej", "gdfgdf");
            //blogpost.AddTag("NoSql");
            //Console.WriteLine(blogpost.Id);
            //ctx.GetCollection<BlogPost>().Add(blogpost);

            //ctx.SaveChanges();
            //Console.Read();

            var query = from blogpost in ctx.GetCollection<BlogPost>().AsQueryable()
                        where blogpost.Comments.Any(c => c.UserName == "roger") && blogpost.CommentCount == 1
                        select blogpost;

            var result = query.ToList();

            foreach (var blogpost in result)
            {
                Console.WriteLine(blogpost.Topic);
            }

        }
    }


    public class BlogPost
    {
        [DocumentId]
        public Guid Id { get; set; }

        public BlogPost()
        {
            Id = Guid.NewGuid();
            Comments = new List<Comment>();
        }

        public string Topic { get; set; }
        public string Body { get; set; }
        public ICollection<Comment> Comments { get; set; }
        public int CommentCount
        {
            get { return Comments.Count; }
        }

        public void ReplyTo(string body,string userName)
        {
            this.Comments.Add(new Comment() {Body = body, UserName = userName});
        }
        public void AddTag(string tag)
        {
        }
    }



    public class Comment
    {
        public string Body { get; set; }
        public string UserName { get; set; }
    }
}