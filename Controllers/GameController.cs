using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CallbreakApp.Data;
using CallbreakApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace CallbreakApp.Controllers;

[Authorize] // all actions require authentication
public class GameController : Controller
{
    private readonly ApplicationDbContext _context;

    public GameController(ApplicationDbContext context)
    {
        _context = context;
    }

    private static double ComputeRoundScore(int bid, int tricks) // for reuse like tricks after updating the bids
    {
        if (bid < 0) bid = 0;
        if (tricks < 0) tricks = 0;
        if (tricks >= bid)
            return bid + 0.1 * (tricks - bid);
        return -(bid - tricks);
    }

    public IActionResult Create() // GET: Empty form to create new game
    {
        return View(new List<string>()); // model for view
    }

    [HttpPost]
    [ValidateAntiForgeryToken] //csrf
    public async Task<IActionResult> Create(List<string> playerNames) // post : bind form
    {
        if (!ModelState.IsValid || playerNames.Count != 4)
        {
            TempData["Error"] = "Enter exactly 4 player names.";
            return View(playerNames);
        }

        var session = new GameSession();
        for (int i = 0; i < 4; i++)
        {
            session.Players.Add(new PlayerSession { Name = playerNames[i] });
        }
        _context.GameSessions.Add(session);
        await _context.SaveChangesAsync();

        HttpContext.Session.SetInt32("CurrentSessionId", session.Id);
        TempData["Success"] = "Game started! Enter bids for Round 1.";

        return RedirectToAction("Bids", new { round = 1 });
    }

    public async Task<IActionResult> Bids(int round) // GET: from with prefill projection
    {
        var sessionId = HttpContext.Session.GetInt32("CurrentSessionId");
        if (sessionId == null) return RedirectToAction("Create");

        var session = await _context.GameSessions.Include(s => s.Players).FirstOrDefaultAsync(s => s.Id == sessionId.Value);
        if (session == null || round > 5 || session.Rounds.Any(r => r.RoundNumber == round))
            return RedirectToAction("Results");

        ViewBag.Round = round;
        ViewBag.Session = session;
        var roundEntity = await _context.Rounds.FirstOrDefaultAsync(r => r.GameSessionId == session.Id && r.RoundNumber == round);
        var prefill = new Dictionary<int, int>();
        if (roundEntity != null)
        {
            var scores = await _context.RoundScores.Where(rs => rs.RoundId == roundEntity.Id).ToListAsync();
            foreach (var rs in scores)
            {
                prefill[rs.PlayerSessionId] = rs.Bid;
            }
        }
        var projectedRound = new Dictionary<int, double>();
        var projectedTotals = new Dictionary<int, double>();
        foreach (var p in session.Players)
        {
            var bid = prefill.GetValueOrDefault(p.Id, 0);
            var proj = ComputeRoundScore(bid, bid);
            projectedRound[p.Id] = proj;
            projectedTotals[p.Id] = p.TotalScore + proj;
        }
        ViewBag.ProjectedRound = projectedRound;
        ViewBag.ProjectedTotals = projectedTotals;

        return View(prefill);
    }

    [HttpPost]
    public async Task<IActionResult> Bids([FromForm] Dictionary<int, int> bids, int round)
    {
        if (bids == null)
            return await Bids(round);

        var sessionId = HttpContext.Session.GetInt32("CurrentSessionId");
        var session = await _context.GameSessions.Include(s => s.Players).FirstOrDefaultAsync(s => s.Id == sessionId.Value);
        if (session == null) return RedirectToAction("Create");

        var playerIds = session.Players.Select(p => p.Id).ToList();
        if (!playerIds.All(id => bids.ContainsKey(id)))
        {
            TempData["Error"] = "Please enter bids for all players.";
            return await Bids(round);
        }

        var sumBids = bids.Values.Sum();
        if (sumBids != 13)
        {
            TempData["Warning"] = sumBids > 13 ? "Total bids >13—invalid, but proceeding." : "Total bids <13—pass allowed.";
        }

        var roundEntity = await _context.Rounds.FirstOrDefaultAsync(r => r.GameSessionId == session.Id && r.RoundNumber == round);
        if (roundEntity == null)
        {
            roundEntity = new Round { GameSessionId = session.Id, RoundNumber = round };
            _context.Rounds.Add(roundEntity);
            await _context.SaveChangesAsync();
        }

        foreach (var kv in bids)
        {
            var playerId = kv.Key;
            var bid = kv.Value;
            var rs = await _context.RoundScores.FirstOrDefaultAsync(x => x.RoundId == roundEntity.Id && x.PlayerSessionId == playerId);
            if (rs == null)
            {
                rs = new RoundScore { RoundId = roundEntity.Id, PlayerSessionId = playerId, Bid = bid, Tricks = 0 };
                _context.RoundScores.Add(rs);
            }
            else
            {
                rs.Bid = bid;
            }
        }
        await _context.SaveChangesAsync();

        var bidsKey = $"RoundBids:{session.Id}:{round}";
        HttpContext.Session.SetString(bidsKey, JsonSerializer.Serialize(bids));

        TempData["Info"] = $"Bids saved for Round {round}. Now enter tricks won.";
        return RedirectToAction("Tricks", new { round });
    }

    public async Task<IActionResult> Tricks(int round)
    {
    var sessionId = HttpContext.Session.GetInt32("CurrentSessionId");
    var session = await _context.GameSessions.Include(s => s.Players).Include(s => s.Rounds).FirstOrDefaultAsync(s => s.Id == sessionId.Value);
    var currentRound = session!.Rounds.First(r => r.RoundNumber == round);


        Dictionary<int, int>? bids = null;
        var roundEntity = await _context.Rounds.FirstOrDefaultAsync(r => r.GameSessionId == session.Id && r.RoundNumber == round);
        if (roundEntity != null)
        {
            var scores = await _context.RoundScores.Where(rs => rs.RoundId == roundEntity.Id).ToListAsync();
            if (scores.Any())
            {
                bids = scores.ToDictionary(x => x.PlayerSessionId, x => x.Bid);
            }
        }
        if (bids == null)
        {
            var bidsKey = $"RoundBids:{session.Id}:{round}";
            var json = HttpContext.Session.GetString(bidsKey);
            if (!string.IsNullOrEmpty(json))
            {
                bids = JsonSerializer.Deserialize<Dictionary<int, int>>(json);
            }
        }

        ViewBag.Round = round;
        ViewBag.Bids = bids ?? new Dictionary<int, int>();
        Dictionary<int, int>? tricks = null;
        bool tricksFromSession = false;
        if (roundEntity != null)
        {
            var scores = await _context.RoundScores.Where(rs => rs.RoundId == roundEntity.Id).ToListAsync();
            if (scores.Any())
            {
                // Only treat RoundScore.Tricks as entered if at least one player has a non-zero value
                var anyNonZero = scores.Any(s => s.Tricks != 0);
                var tricksKey = $"RoundTricks:{session.Id}:{round}";
                var tjson = HttpContext.Session.GetString(tricksKey);
                if (!string.IsNullOrEmpty(tjson))
                {
                    tricks = JsonSerializer.Deserialize<Dictionary<int, int>>(tjson);
                    tricksFromSession = true;
                }
                else if (anyNonZero)
                {
                    tricks = scores.ToDictionary(x => x.PlayerSessionId, x => x.Tricks);
                }
                // if no non-zero tricks and no session value, leave tricks null so view will default to bids
            }
        }
        if (tricks == null && !tricksFromSession)
        {
            var tricksKey = $"RoundTricks:{session.Id}:{round}";
            var tjson = HttpContext.Session.GetString(tricksKey);
            if (!string.IsNullOrEmpty(tjson))
            {
                tricks = JsonSerializer.Deserialize<Dictionary<int, int>>(tjson);
            }
        }

        ViewBag.Tricks = tricks ?? new Dictionary<int, int>();
        ViewBag.Session = session;
        var projectedRound2 = new Dictionary<int, double>();
        var projectedTotals2 = new Dictionary<int, double>();
        foreach (var p in session.Players)
        {
            var bid = (bids != null && bids.ContainsKey(p.Id)) ? bids[p.Id] : 0;
            var t = (tricks != null && tricks.ContainsKey(p.Id)) ? tricks[p.Id] : 0;
            var proj = ComputeRoundScore(bid, t);
            projectedRound2[p.Id] = proj;
            projectedTotals2[p.Id] = p.TotalScore + proj;
        }
        ViewBag.ProjectedRound = projectedRound2;
        ViewBag.ProjectedTotals = projectedTotals2;
        return View(tricks ?? new Dictionary<int, int>());
    }

    [HttpPost]
    public async Task<IActionResult> Tricks([FromForm] Dictionary<int, int> tricks, int round)
    {
        if (tricks == null)
            return await Tricks(round);

        var sessionId = HttpContext.Session.GetInt32("CurrentSessionId");
        var session = await _context.GameSessions.Include(s => s.Players).Include(s => s.Rounds).FirstOrDefaultAsync(s => s.Id == sessionId.Value);
        var currentRound = session!.Rounds.First(r => r.RoundNumber == round);

        var tricksKey = $"RoundTricks:{session.Id}:{round}";
        HttpContext.Session.SetString(tricksKey, JsonSerializer.Serialize(tricks));

        var playerIds = session.Players.Select(p => p.Id).ToList();
        if (!playerIds.All(id => tricks.ContainsKey(id)))
        {
            TempData["Error"] = "Please enter tricks for all players.";
            return await Tricks(round);
        }

        var roundEntity = await _context.Rounds.FirstOrDefaultAsync(r => r.GameSessionId == session.Id && r.RoundNumber == round);
        if (roundEntity == null)
        {
            roundEntity = new Round { GameSessionId = session.Id, RoundNumber = round };
            _context.Rounds.Add(roundEntity);
            await _context.SaveChangesAsync();
        }

        foreach (var kv in tricks)
        {
            var playerId = kv.Key;
            var t = kv.Value;
            var rs = await _context.RoundScores.FirstOrDefaultAsync(x => x.RoundId == roundEntity.Id && x.PlayerSessionId == playerId);
            if (rs == null)
            {
                rs = new RoundScore { RoundId = roundEntity.Id, PlayerSessionId = playerId, Bid = 0, Tricks = t };
                _context.RoundScores.Add(rs);
            }
            else
            {
                rs.Tricks = t;
            }
        }
        await _context.SaveChangesAsync();

        var allScores = await _context.RoundScores.Where(rs => rs.RoundId == roundEntity.Id).ToListAsync();
        currentRound.Bids = allScores.ToDictionary(x => x.PlayerSessionId, x => x.Bid);
        currentRound.Tricks = allScores.ToDictionary(x => x.PlayerSessionId, x => x.Tricks);

        var sumTricks = tricks.Values.Sum();
        if (sumTricks != 13)
        {
            TempData["Warning"] = "Total tricks !=13—proceeding anyway.";
        }

        try
        {
            currentRound.CalculateScores();
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Score calc error: {ex.Message}";
            return await Tricks(round);
        }

        return RedirectToAction("Confirm", new { round });
    }

    public async Task<IActionResult> Confirm(int round, bool? confirm = null)
    {
        var sessionId = HttpContext.Session.GetInt32("CurrentSessionId");
        var session = await _context.GameSessions.Include(s => s.Players).Include(s => s.Rounds).FirstOrDefaultAsync(s => s.Id == sessionId.Value);
        var currentRound = session!.Rounds.First(r => r.RoundNumber == round);

        if (confirm == true)
        {
            currentRound.IsConfirmed = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Round {round} locked!";

            if (round < 5)
            {
                return RedirectToAction("Bids", new { round = round + 1 });
            }
            else
            {
                session.IsCompleted = true;
                await _context.SaveChangesAsync();
                return RedirectToAction("Results");
            }
        }

        ViewBag.Round = round;
        ViewBag.Session = session;
        return View();
    }

    public async Task<IActionResult> Results()
    {
        var sessionId = HttpContext.Session.GetInt32("CurrentSessionId");
    var session = await _context.GameSessions.Include(s => s.Players).Include(s => s.Rounds).FirstOrDefaultAsync(s => s.Id == sessionId.Value);
        if (session == null) return RedirectToAction("Create");

        var winner = session.GetWinner();
        ViewBag.Winner = winner?.Name;
        ViewBag.Session = session;
        return View();
    }
}