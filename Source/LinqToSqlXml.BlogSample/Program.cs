using System;
using System.Collections.Generic;
using System.Linq;
using LinqToSqlXml;

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

            IQueryable<BlogPost> query = from blogpost in ctx.GetCollection<BlogPost>().AsQueryable()
                                         where
                                             blogpost.Comments.Any(c => c.UserName == "roger") &&
                                             blogpost.CommentCount == 1
                                         select blogpost;

            List<BlogPost> result = query.ToList();

            foreach (BlogPost blogpost in result)
            {
                Console.WriteLine(blogpost.Topic);
            }
        }
    }


    public class BlogPost
    {
        public BlogPost()
        {
            Id = Guid.NewGuid();
            Comments = new List<Comment>();
        }

        [DocumentId]
        public Guid Id { get; set; }

        public string Topic { get; set; }
        public string Body { get; set; }
        public ICollection<Comment> Comments { get; set; }

        public int CommentCount
        {
            get { return Comments.Count; }
        }

        public void ReplyTo(string body, string userName)
        {
            Comments.Add(new Comment {Body = body, UserName = userName});
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