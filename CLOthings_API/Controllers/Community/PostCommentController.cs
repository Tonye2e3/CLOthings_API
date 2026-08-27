using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CLOthings_API.Models;

[Route("api/[controller]")]
[ApiController]
public class PostCommentController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public PostCommentController(CLOthingsContext context)
    {
        _context = context;
    }

    // GET: api/PostComment/post/5
    // 依 communityPostId 查詢「這篇貼文底下的所有留言」，PostDetailView.vue 要用這個端點。
    // 依留言時間新到舊排序，最新的留言排最前面。
    [HttpGet("post/{communitypostid}")]
    public async Task<IEnumerable<CommentDTO>> GetPostCommentByPost(int communitypostid)
    {
        return await _context.PostComment
            .Where(c => c.CommunityPostId == communitypostid)
            .OrderByDescending(c => c.CommentDate)
            .Select(c => new CommentDTO
            {
                PostCommentId = c.PostCommentId,
                ParentCommentId = c.ParentCommentId,
                CommunityPostId = c.CommunityPostId,
                UserId = c.UserId,
                User = c.User.Username,
                Avatar = c.User.UserProfile.Select(p => p.Avatar).FirstOrDefault(),
                CommentText = c.CommentText,
                CommentDate = c.CommentDate
            })
            .ToListAsync();
    }

    // GET: api/PostComment/5
    [HttpGet("{postcommentid}")]
    public async Task<CommentDTO> GetPostComment(int postcommentid)
    {
        var comment = await _context.PostComment
            .Where(c => c.PostCommentId == postcommentid)
            .Select(c => new CommentDTO
            {
                PostCommentId = c.PostCommentId,
                ParentCommentId = c.ParentCommentId,
                CommunityPostId = c.CommunityPostId,
                UserId = c.UserId,
                User = c.User.Username,
                Avatar = c.User.UserProfile.Select(p => p.Avatar).FirstOrDefault(),
                CommentText = c.CommentText,
                CommentDate = c.CommentDate
            })
            .FirstOrDefaultAsync();

        if (comment == null)
        {
            return null;
        }

        return comment;
    }

    // PUT: api/PostComment/5
    // 加 [Authorize]：原本這支完全沒有登入檢查，任何人（不用登入）都能改任何一則留言的內容，
    // 這次補上——並且加上「這則留言是不是自己寫的」比對，不是自己的留言不能改（除非是管理員）。
    [HttpPut("{postcommentid}")]
    [Authorize]
    public async Task<ResultDTO> PutPostComment(int? postcommentid, CommentDTO commentDTO)
    {
        if (postcommentid != commentDTO.PostCommentId)
        {
            return new ResultDTO { OK = false, Code = 400 };
        }

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        PostComment comment = await _context.PostComment.FindAsync(commentDTO.PostCommentId);
        if (comment == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        if (comment.UserId != currentUserId.Value && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            return new ResultDTO { OK = false, Code = 403 };
        }
        else
        {
            // 只能改留言文字本身，User、Avatar 是查詢時組出來的唯讀資訊，不能寫回資料庫。
            comment.CommentText = commentDTO.CommentText;
            _context.Entry(comment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return new ResultDTO { OK = false, Code = 500 };
            }
            return new ResultDTO { OK = true, Code = 204 };
        }
    }

    // POST: api/PostComment
    // 加 [Authorize]：留言一定要登入。
    //
    // 資安修正：原本直接相信前端 request body 裡的 commentDTO.UserId，代表誰是「留言的人」——
    // 改成一律從登入用的 JWT Token 解出真正的身分，不管前端傳什麼都直接蓋掉，
    // 「留言的人是誰」只有伺服器自己說了算。
    [HttpPost]
    [Authorize]
    public async Task<ResultDTO> PostPostComment(CommentDTO commentDTO)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        PostComment comment = new PostComment
        {
            PostCommentId = 0,
            ParentCommentId = commentDTO.ParentCommentId,
            CommunityPostId = commentDTO.CommunityPostId,
            UserId = currentUserId.Value,
            CommentText = commentDTO.CommentText,
            CommentDate = DateTimeOffset.Now
        };
        _context.PostComment.Add(comment);
        await _context.SaveChangesAsync();

        // 留言成功後，順便通知這篇貼文的作者——除非「自己留言自己的貼文」，
        // 那種情況不用通知自己。要先查出這篇貼文是誰發的才知道通知要給誰。
        var post = await _context.CommunityPost.FindAsync(commentDTO.CommunityPostId);
        if (post != null && post.UserId != currentUserId.Value)
        {
            _context.Notification.Add(new Notification
            {
                UserId = post.UserId,
                FromUserId = currentUserId.Value,
                Type = "comment",
                CommunityPostId = commentDTO.CommunityPostId,
                CreatedDate = DateTimeOffset.Now,
                IsRead = false
            });
            await _context.SaveChangesAsync();
        }

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/PostComment/5
    // 加 [Authorize]：原本這支跟 PUT 一樣完全沒有登入檢查，任何人都能刪除任何一則留言，
    // 這次補上，並且加上「這則留言是不是自己寫的」比對（管理員可以刪任何留言，方便後台審核）。
    [HttpDelete("{postcommentid}")]
    [Authorize]
    public async Task<ResultDTO> DeletePostComment(int? postcommentid)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return new ResultDTO { OK = false, Code = 401 };
        }

        var comment = await _context.PostComment.FindAsync(postcommentid);
        if (comment == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        if (comment.UserId != currentUserId.Value && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            return new ResultDTO { OK = false, Code = 403 };
        }
        try
        {
            _context.PostComment.Remove(comment);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return new ResultDTO { OK = false, Code = 500 };
        }
        return new ResultDTO { OK = true, Code = 204 };
    }

    // GetCurrentUserId：跟 ChatController.cs 是同一套寫法，從登入用的 JWT Token 裡取出 userId，
    // 不相信前端自己送來的任何身分欄位。
    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : null;
    }
}