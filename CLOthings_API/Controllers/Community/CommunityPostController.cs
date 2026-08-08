using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class CommunityPostController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public CommunityPostController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/CommunityPost
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CommunityPost>>> GetCommunityPost()
    {
        return await _context.CommunityPost.ToListAsync();
    }

    // GET: api/CommunityPost/5
    [HttpGet("{communitypostid}")]
    public async Task<ActionResult<CommunityPost>> GetCommunityPost(int communitypostid)
    {
        var communitypost = await _context.CommunityPost.FindAsync(communitypostid);

        if (communitypost == null)
        {
            return NotFound();
        }

        return communitypost;
    }

    // PUT: api/CommunityPost/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{communitypostid}")]
    public async Task<IActionResult> PutCommunityPost(int? communitypostid, CommunityPost communitypost)
    {
        if (communitypostid != communitypost.CommunityPostId)
        {
            return BadRequest();
        }

        _context.Entry(communitypost).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!CommunityPostExists(communitypostid))
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

    // POST: api/CommunityPost
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    public async Task<ActionResult<CommunityPost>> PostCommunityPost(CommunityPost communitypost)
    {
        _context.CommunityPost.Add(communitypost);
        await _context.SaveChangesAsync();

        return CreatedAtAction("GetCommunityPost", new { communitypostid = communitypost.CommunityPostId }, communitypost);
    }

    // DELETE: api/CommunityPost/5
    [HttpDelete("{communitypostid}")]
    public async Task<IActionResult> DeleteCommunityPost(int? communitypostid)
    {
        var communitypost = await _context.CommunityPost.FindAsync(communitypostid);
        if (communitypost == null)
        {
            return NotFound();
        }

        _context.CommunityPost.Remove(communitypost);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool CommunityPostExists(int? communitypostid)
    {
        return _context.CommunityPost.Any(e => e.CommunityPostId == communitypostid);
    }
}
