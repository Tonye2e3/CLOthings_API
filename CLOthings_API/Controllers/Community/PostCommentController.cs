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
    [HttpPut("{postcommentid}")]
    public async Task<ResultDTO> PutPostComment(int? postcommentid, CommentDTO commentDTO)
    {
        if (postcommentid != commentDTO.PostCommentId)
        {
            return new ResultDTO { OK = false, Code = 400 };
        }

        PostComment comment = await _context.PostComment.FindAsync(commentDTO.PostCommentId);
        if (comment == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
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
    [HttpPost]
    [Authorize]
    public async Task<ResultDTO> PostPostComment(CommentDTO commentDTO)
    {
        PostComment comment = new PostComment
        {
            PostCommentId = 0,
            ParentCommentId = commentDTO.ParentCommentId,
            CommunityPostId = commentDTO.CommunityPostId,
            UserId = commentDTO.UserId,
            CommentText = commentDTO.CommentText,
            CommentDate = DateTimeOffset.Now
        };
        _context.PostComment.Add(comment);
        await _context.SaveChangesAsync();

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/PostComment/5
    [HttpDelete("{postcommentid}")]
    public async Task<ResultDTO> DeletePostComment(int? postcommentid)
    {
        var comment = await _context.PostComment.FindAsync(postcommentid);
        if (comment == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
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
}