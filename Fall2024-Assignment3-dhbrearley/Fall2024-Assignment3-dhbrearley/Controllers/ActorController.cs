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
    public class ActorController : Controller
    {
        private readonly string _apiKey;
        private readonly string _apiEndpoint;        
        private const string AiDeployment = "gpt-35-turbo";
        private readonly ApiKeyCredential _apiCredential;
        private readonly ApplicationDbContext _context;
        public ActorController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _apiKey = configuration["ApiKey"];
            _apiEndpoint = configuration["ApiEndpoint"];
            _apiCredential = new ApiKeyCredential(_apiKey);
        }

        // GET: Movie/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Movie/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Gender,Age,Link")] Actor actor, IFormFile? Photo) {
            if (ModelState.IsValid) {
                if (Photo != null && Photo.Length > 0) {
                    using var memoryStream = new MemoryStream();
                    Photo.CopyTo(memoryStream);
                    actor.Photo = memoryStream.ToArray();

                    _context.Add(actor);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                else {
                     ModelState.AddModelError(nameof(actor.Photo), "Photo is required.");
                }
            }
            return View(actor);
        }
        public async Task<IActionResult> GetActorPhoto(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var actor = await _context.Actor.FindAsync(id);
            if (actor == null || actor.Photo == null)
            {
                return NotFound();
            }

            var data = actor.Photo;
            return File(data, "image/jpg");
        }

        // GET
        public async Task<IActionResult> Index()
        {
            return View(await _context.Actor.ToListAsync());
        }

        // GET: Actor/Details/5
        public async Task<IActionResult> Details(int? id) {
        if (id == null) return NotFound();

        var actor = await _context.Actor.FirstOrDefaultAsync(m => m.Id == id);
        if (actor == null) return NotFound();

        var movies = await _context.MovieActor
            .Include(cs => cs.Movie)
            .Where(cs => cs.ActorId == actor.Id)
            .Select(cs => cs.Movie)
            .ToListAsync();

        var (tweets, sentimentScores, sentimentAverage) = await GenerateAiTweets(actor.Name);

        var vm = new ActorDetailsView(actor, movies, tweets, sentimentScores, sentimentAverage);
        return View(vm);
        }

        private async Task<(List<Tweet>, List<double> sentimentScores, double sentimentAverage)> GenerateAiTweets(string actorName) {
            ChatClient client = new AzureOpenAIClient(new Uri(_apiEndpoint), _apiCredential).GetChatClient(AiDeployment);

            var messages = new ChatMessage[]
            {
                new SystemChatMessage("As Twitter, generate a single-line, valid JSON array of tweet objects. Each object should have 'username' and 'text' fields, formatted as follows: [{\"username\":\"@ExampleUser\",\"text\":\"Sample tweet text\"},{...}]. Ensure no spaces are between objects, and the JSON array begins with '[' and ends with ']'. Generate 20 unique tweets about the specified actor, with varied usernames and tweet content."),
                new UserChatMessage($"Actor: {actorName}")
            };

            int retryCount = 0;
            const int maxRetries = 5;
            string tweetsJsonString = "[]";
            JsonArray jsonArray = new JsonArray();

            while (retryCount < maxRetries)
            {
                var result = await client.CompleteChatAsync(messages);
                tweetsJsonString = result.Value.Content.FirstOrDefault()?.Text ?? "[]";

                try
                {
                    jsonArray = JsonNode.Parse(tweetsJsonString)?.AsArray() ?? new JsonArray();
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
                return (new List<Tweet>(), new List<double>(), 0.0);;
            }

            var analyzer = new SentimentIntensityAnalyzer();
            var tweets = new List<Tweet>();
            var sentimentScores = new List<double>();
            double sentimentTotal = 0;

            foreach (var item in jsonArray)
            {
                var tweet = new Tweet
                {
                    Username = item["username"]?.ToString() ?? "Anonymous",
                    Text = item["text"]?.ToString() ?? ""
                };
                tweets.Add(tweet);

                var sentimentScore = analyzer.PolarityScores(tweet.Text).Compound;
                sentimentScores.Add(sentimentScore);
                sentimentTotal += sentimentScore;
            }

             double sentimentAverage = sentimentScores.Count > 0 ? sentimentTotal / sentimentScores.Count : 0;

            return (tweets, sentimentScores, sentimentAverage);

        }

        // GET: Actor/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var actor = await _context.Actor.FindAsync(id);
            if (actor == null)
            {
                return NotFound();
            }
            return View(actor);
        }

        // POST: Actor/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Gender,Age,Link")] Actor actor, IFormFile? Photo)
        {
            if (id != actor.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Retrieve the existing actor from the database
                    var existingActor = await _context.Actor.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
                    if (existingActor == null)
                    {
                        return NotFound();
                    }

                    // Update fields from the form
                    existingActor.Name = actor.Name;
                    existingActor.Gender = actor.Gender;
                    existingActor.Age = actor.Age;
                    existingActor.Link = actor.Link;

                    // Handle photo upload
                    if (Photo != null && Photo.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await Photo.CopyToAsync(memoryStream);
                        existingActor.Photo = memoryStream.ToArray(); // Update the photo only if a new one is provided
                    }

                    _context.Update(existingActor);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ActorExists(actor.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(actor);
        }


        // POST: Actor/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var actor = await _context.Actor.FindAsync(id);
            if (actor != null)
            {
                _context.Actor.Remove(actor);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ActorExists(int id)
        {
            return _context.Actor.Any(e => e.Id == id);
        }
    }
}
