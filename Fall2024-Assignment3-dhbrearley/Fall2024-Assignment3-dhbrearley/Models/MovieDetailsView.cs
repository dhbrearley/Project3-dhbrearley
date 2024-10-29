using System.Collections.Generic;

namespace Fall2024_Assignment3_dhbrearley.Models
{
    public class Review
    {
        public string Username { get; set; }
        public string Text { get; set; }
    }
    public class MovieDetailsView
    {
        public Movie Movie { get; set; }
        public IEnumerable<Actor> Actors { get; set; }
        public List<Review> Reviews { get; set; } = new List<Review>();

        public List<double> SentimentScores { get; set; } = new List<double>();

        public double SentimentAverage { get; set; }

        public MovieDetailsView(Movie movie, IEnumerable<Actor> actors, List<Review> reviews, List<double> sentimentScores, double sentimentAverage)
        {
            Movie = movie;
            Actors = actors;
            Reviews = reviews;
            SentimentScores = sentimentScores;
            SentimentAverage = sentimentAverage;
        }
    }
}