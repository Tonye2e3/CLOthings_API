using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class CommunityFavoriteController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public CommunityFavoriteController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/CommunityFavorite
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CommunityFavorite>>> GetCommunityFavorite()
    {
        return await _context.CommunityFavorite.ToListAsync();
    }

    // GET: api/CommunityFavorite/5
    [HttpGet("{communityfavoriteid}")]
    public async Task<ActionResult<CommunityFavorite>> GetCommunityFavorite(int communityfavoriteid)
    {
        var communityfavorite = await _context.CommunityFavorite.FindAsync(communityfavoriteid);

        if (communityfavorite == null)
        {
            return NotFound();
        }

        return communityfavorite;
    }

    // PUT: api/CommunityFavorite/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{communityfavoriteid}")]
    public async Task<IActionResult> PutCommunityFavorite(int? communityfavoriteid, CommunityFavorite communityfavorite)
    {
        if (communityfavoriteid != communityfavorite.CommunityFavoriteId)
        {
            return BadRequest();
        }

        _context.Entry(communityfavorite).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!CommunityFavoriteExists(communityfavoriteid))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    // POST: api/CommunityFavorite
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    public async Task<ActionResult<CommunityFavorite>> PostCommunityFavorite(CommunityFavorite communityfavorite)
    {
        _context.CommunityFavorite.Add(communityfavorite);
        await _context.SaveChangesAsync();

        return CreatedAtAction("GetCommunityFavorite", new { communityfavoriteid = communityfavorite.CommunityFavoriteId }, communityfavorite);
    }

    // DELETE: api/CommunityFavorite/5
    [HttpDelete("{communityfavoriteid}")]
    public async Task<IActionResult> DeleteCommunityFavorite(int? communityfavoriteid)
    {
        var communityfavorite = await _context.CommunityFavorite.FindAsync(communityfavoriteid);
        if (communityfavorite == null)
        {
            return NotFound();
        }

        _context.CommunityFavorite.Remove(communityfavorite);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool CommunityFavoriteExists(int? communityfavoriteid)
    {
        return _context.CommunityFavorite.Any(e => e.CommunityFavoriteId == communityfavoriteid);
    }
}
