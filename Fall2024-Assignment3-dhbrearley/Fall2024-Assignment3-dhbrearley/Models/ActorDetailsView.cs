using System.Collections.Generic;

namespace Fall2024_Assignment3_dhbrearley.Models
{
    public class Tweet
    {
        public string Username { get; set; }
        public string Text { get; set; }
    }
    public class ActorDetailsView
    {
        public Actor Actor { get; set; }
        public IEnumerable<Movie> Movies { get; set; }
        public List<Tweet> Tweets { get; set; } = new List<Tweet>();
        public List<double> SentimentScores { get; set; } = new List<double>();

        public double SentimentAverage { get; set; }

        public ActorDetailsView(Actor actor, IEnumerable<Movie> movies, List<Tweet> tweets, List<double> sentimentScores, double sentimentAverage)
        {
            Actor = actor;
            Movies = movies;
            Tweets = tweets;
            SentimentScores = sentimentScores;
            SentimentAverage = sentimentAverage;
        }
    }
}