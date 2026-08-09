
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;

public class PostCommentController : Controller
{
    private readonly CLOthingsContext _context;

    public PostCommentController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: POSTCOMMENTS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.PostComment.ToListAsync());
    }

    // GET: POSTCOMMENTS/Details/5
    public async Task<IActionResult> Details(int? postcommentid)
    {
        if (postcommentid == null)
        {
            return NotFound();
        }

        var postcomment = await _context.PostComment
            .FirstOrDefaultAsync(m => m.PostCommentId == postcommentid);
        if (postcomment == null)
        {
            return NotFound();
        }

        return View(postcomment);
    }

    // GET: POSTCOMMENTS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: POSTCOMMENTS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("PostCommentId,ParentCommentId,CommunityPostId,UserId,CommentText,CommentDate,CommunityPost,InverseParentComment,ParentComment,User")] PostComment postcomment)
    {
        if (ModelState.IsValid)
        {
            _context.Add(postcomment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(postcomment);
    }

    // GET: POSTCOMMENTS/Edit/5
    public async Task<IActionResult> Edit(int? postcommentid)
    {
        if (postcommentid == null)
        {
            return NotFound();
        }

        var postcomment = await _context.PostComment.FindAsync(postcommentid);
        if (postcomment == null)
        {
            return NotFound();
        }
        return View(postcomment);
    }

    // POST: POSTCOMMENTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? postcommentid, [Bind("PostCommentId,ParentCommentId,CommunityPostId,UserId,CommentText,CommentDate,CommunityPost,InverseParentComment,ParentComment,User")] PostComment postcomment)
    {
        if (postcommentid != postcomment.PostCommentId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(postcomment);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PostCommentExists(postcomment.PostCommentId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(postcomment);
    }

    // GET: POSTCOMMENTS/Delete/5
    public async Task<IActionResult> Delete(int? postcommentid)
    {
        if (postcommentid == null)
        {
            return NotFound();
        }

        var postcomment = await _context.PostComment
            .FirstOrDefaultAsync(m => m.PostCommentId == postcommentid);
        if (postcomment == null)
        {
            return NotFound();
        }

        return View(postcomment);
    }

    // POST: POSTCOMMENTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? postcommentid)
    {
        var postcomment = await _context.PostComment.FindAsync(postcommentid);
        if (postcomment != null)
        {
            _context.PostComment.Remove(postcomment);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool PostCommentExists(int? postcommentid)
    {
        return _context.PostComment.Any(e => e.PostCommentId == postcommentid);
    }
}
