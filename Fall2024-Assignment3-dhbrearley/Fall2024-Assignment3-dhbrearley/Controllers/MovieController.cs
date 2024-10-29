using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Fall2024_Assignment3_dhbrearley.Data;
using Fall2024_Assignment3_dhbrearley.Models;
using System.Text.Json.Nodes;
using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using VaderSharp2;
using System.Text.Json;

namespace Fall2024_Assignment3_dhbrearley.Controllers
{
    public class MovieController : Controller
    {
        private readonly string _apiKey;
        private readonly string _apiEndpoint;
        private const string AiDeployment = "gpt-35-turbo";
        private readonly ApiKeyCredential _apiCredential;
        private readonly ApplicationDbContext _context;

        public MovieController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _apiKey = configuration["ApiKey"];
            _apiEndpoint = configuration["ApiEndpoint"];
            _apiCredential = new ApiKeyCredential(_apiKey);
        }

        // GET
        public async Task<IActionResult> Index()
        {
            return View(await _context.Movie.ToListAsync());
        }

        // GET: Movie/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie
                .FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }

            var actors = await _context.MovieActor
                .Include(cs => cs.Actor)
                .Where(cs => cs.MovieId == movie.Id)
                .Select(cs => cs.Actor)
                .ToListAsync();
            
            var (reviews, sentimentScores, sentimentAverage) = await GenerateAiReviews(movie.Title);

            var vm = new MovieDetailsView(movie, actors, reviews, sentimentScores, sentimentAverage);

            return View(vm);
        }

        private async Task<(List<Review> reviews, List<double> sentimentScores, double sentimentAverage)> GenerateAiReviews(string movieTitle)
        {
            var client = new AzureOpenAIClient(new Uri(_apiEndpoint), _apiCredential).GetChatClient(AiDeployment);

            var messages = new ChatMessage[]
            {
                new SystemChatMessage("As Twitter, generate a single-line, valid JSON array of review objects. Each object should have 'username' and 'text' fields, formatted as follows: [{\"username\":\"@ExampleUser\",\"text\":\"Sample review text\"},{...}]. Ensure no spaces are between objects, and the JSON array begins with '[' and ends with ']'. Generate 10 unique reviews about the specified movie, with varied usernames and review content."),
                new UserChatMessage($"Movie: {movieTitle}")
            };

            int retryCount = 0;
            const int maxRetries = 5;
            string reviewsJsonString = "[]";
            JsonArray jsonArray = new JsonArray();

            while (retryCount < maxRetries)
            {
                var result = await client.CompleteChatAsync(messages);
                reviewsJsonString = result.Value.Content.FirstOrDefault()?.Text ?? "[]";

                try
                {
                    jsonArray = JsonNode.Parse(reviewsJsonString)?.AsArray() ?? new JsonArray();
                    break;
                }
                catch (JsonException)
                {
                    Console.WriteLine("JSON Parsing Error. Retrying...");
                    retryCount++;
                    await Task.Delay(500);
                }
            }

            if (retryCount == maxRetries)
            {
                Console.WriteLine("Failed to receive valid JSON after multiple attempts.");
                return (new List<Review>(), new List<double>(), 0.0);
            }

            var analyzer = new SentimentIntensityAnalyzer();
            var reviews = new List<Review>();
            var sentimentScores = new List<double>();
            double sentimentTotal = 0;

            foreach (var item in jsonArray)
            {
                var review = new Review
                {
                    Username = item["username"]?.ToString() ?? "Anonymous",
                    Text = item["text"]?.ToString() ?? ""
                };
                reviews.Add(review);

                var sentimentScore = analyzer.PolarityScores(review.Text).Compound;
                sentimentScores.Add(sentimentScore);
                sentimentTotal += sentimentScore;
            }

             double sentimentAverage = sentimentScores.Count > 0 ? sentimentTotal / sentimentScores.Count : 0;

            return (reviews, sentimentScores, sentimentAverage);
        }

        // GET: Movie/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Movie/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Link,Genre,Release")] Movie movie, IFormFile? Photo) {
            if (ModelState.IsValid) {
                if (Photo != null && Photo.Length > 0) {
                    using var memoryStream = new MemoryStream();
                    Photo.CopyTo(memoryStream);
                    movie.Photo = memoryStream.ToArray();

                    _context.Add(movie);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                else {
                     ModelState.AddModelError(nameof(movie.Photo), "Photo is required.");
                }
            }
            return View(movie);
        }

        // GET: Movie/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie.FindAsync(id);
            if (movie == null)
            {
                return NotFound();
            }
            return View(movie);
        }

        // POST: Movie/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Link,Genre,Release")] Movie movie, IFormFile? Photo)
        {
            if (id != movie.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Retrieve the existing movie from the database
                    var existingMovie = await _context.Movie.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
                    if (existingMovie == null)
                    {
                        return NotFound();
                    }

                    // Update fields from the form
                    existingMovie.Title = movie.Title;
                    existingMovie.Link = movie.Link;
                    existingMovie.Genre = movie.Genre;
                    existingMovie.Release = movie.Release;

                    // Handle photo upload
                    if (Photo != null && Photo.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await Photo.CopyToAsync(memoryStream);
                        existingMovie.Photo = memoryStream.ToArray(); // Update the photo only if a new one is provided
                    }

                    _context.Update(existingMovie);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieExists(movie.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(movie);
        }

        // GET: Movie/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie
                .FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }

            return View(movie);
        }

        // POST: Movie/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movie = await _context.Movie.FindAsync(id);
            if (movie != null)
            {
                _context.Movie.Remove(movie);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MovieExists(int id)
        {
            return _context.Movie.Any(e => e.Id == id);
        }
    }
}
