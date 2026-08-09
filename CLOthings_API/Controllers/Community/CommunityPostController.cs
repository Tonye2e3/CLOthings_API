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
    public async Task<ActionResult<IEnumerable<CommunityPostDTO>>> GetCommunityPost()
    {
        return await _context.CommunityPost
            .Select(c => new CommunityPostDTO
            {
                CommunityPostId = c.CommunityPostId,
                UserId = c.UserId,
                Content = c.Content,
                PostDate = c.PostDate,
                Status = c.Status,
                User = new UserSummaryDTO
                {
                    UserId = c.User.UserId,
                    Name = c.User.Username,
                    Avatar = c.User.UserProfile.Select(p => p.Avatar).FirstOrDefault()
                },
                Images = c.PostImage
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new PostImageDTO
                    {
                        PostImageId = i.PostImageId,
                        ImageFileName = i.ImageFileName,
                        SortOrder = i.SortOrder
                    }).ToList(),
                LikesCount = c.PostLike.Count(),
                CommentsCount = c.PostComment.Count(),
                TaggedProducts = c.PostTaggedProduct
                    .Select(t => new TaggedProductDTO
                    {
                        PostTaggedProductId = t.PostTaggedProductId,
                        ProductId = t.ProductId,
                        ProductRoute = t.ProductRoute,
                        Name = t.Product.ProductName
                    }).ToList()
            })
            .ToListAsync();
    }

    // GET: api/CommunityPost/5
    [HttpGet("{communitypostid}")]
    public async Task<ActionResult<CommunityPost>> GetCommunityPost(int communitypostid)
    {
        var communitypost = await _context.CommunityPost.FindAsync(communitypostid);

        if (communitypost == null)
        {
            return NotFound();   // HTTP Error : 400
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
            return BadRequest();                // HTTP Error:400
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
                return NotFound();              //HTTP Error:404
            }
            else
            {
                throw;
            }
        }

        return NoContent();                     //HTTP :204
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
