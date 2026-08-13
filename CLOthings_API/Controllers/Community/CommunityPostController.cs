using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;

[Route("api/[controller]")]
[ApiController]
public class CommunityPostController : ControllerBase
{
    private readonly CLOthingsContext _context;
    public CommunityPostController(CLOthingsContext context)
    {
        _context = context;
    }

    // POST: api/CommunityPost/upload-images
    // 加 [Authorize]：上傳圖片是「發文」流程的一部分，沒登入不該能上傳檔案佔用你的硬碟空間。
    [HttpPost("upload-images")]
    [Authorize]
    public async Task<ActionResult<List<string>>> UploadImages(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest();
        }

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "posts");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var savedPaths = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                continue;
            }

            var extension = Path.GetExtension(file.FileName);
            var newFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(folder, newFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            savedPaths.Add($"/images/posts/{newFileName}");
        }

        return Ok(savedPaths);
    }

    // GET: api/CommunityPost
    // 拿掉 [Authorize]：貼文列表是社群首頁動態牆要用的，任何人（不管有沒有登入）都該看得到，
    // 不能設成一定要登入才能瀏覽。
    [HttpGet]
    public async Task<IEnumerable<CommunityPostDTO>> GetCommunityPost()
    {
        return await _context.CommunityPost.Select(c => new CommunityPostDTO
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
                    Name = t.Product.ProductName,
                    Image = t.Product.ProductImg.Select(pi => pi.ProductImgFile).FirstOrDefault(),
                    Price = t.Product.Price
                }).ToList()
        }).ToListAsync();
    }

    // GET: api/CommunityPost/5
    // 一樣不用登入，貼文詳細頁任何人都該看得到。
    [HttpGet("{communitypostid}")]
    public async Task<CommunityPostDTO> GetCommunityPost(int communitypostid)
    {
        var communitypost = await _context.CommunityPost
            .Where(c => c.CommunityPostId == communitypostid)
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
                        Name = t.Product.ProductName,
                        Image = t.Product.ProductImg.Select(pi => pi.ProductImgFile).FirstOrDefault(),
                        Price = t.Product.Price
                    }).ToList()
            })
            .FirstOrDefaultAsync();

        if (communitypost == null)
        {
            return null;
        }

        return communitypost;
    }

    // GET: api/CommunityPost/user/5
    // 一樣不用登入，個人頁瀏覽別人的貼文也不用先登入。
    [HttpGet("user/{userid}")]
    public async Task<IEnumerable<CommunityPostDTO>> GetCommunityPostByUser(int userid)
    {
        return await _context.CommunityPost
            .Where(c => c.UserId == userid)
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
                        Name = t.Product.ProductName,
                        Image = t.Product.ProductImg.Select(pi => pi.ProductImgFile).FirstOrDefault(),
                        Price = t.Product.Price
                    }).ToList()
            })
            .ToListAsync();
    }

    // GET: api/CommunityPost/similar/5
    // 一樣不用登入。
    [HttpGet("similar/{communitypostid}")]
    public async Task<IEnumerable<CommunityPostDTO>> GetSimilarCommunityPost(int communitypostid)
    {
        var taggedProductIds = await _context.PostTaggedProduct
            .Where(t => t.CommunityPostId == communitypostid)
            .Select(t => t.ProductId)
            .ToListAsync();

        var similarPostIds = await _context.PostTaggedProduct
            .Where(t => taggedProductIds.Contains(t.ProductId) && t.CommunityPostId != communitypostid)
            .Select(t => t.CommunityPostId)
            .Distinct()
            .Take(3)
            .ToListAsync();

        return await _context.CommunityPost
            .Where(c => similarPostIds.Contains(c.CommunityPostId))
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
                        Name = t.Product.ProductName,
                        Image = t.Product.ProductImg.Select(pi => pi.ProductImgFile).FirstOrDefault(),
                        Price = t.Product.Price
                    }).ToList()
            })
            .ToListAsync();
    }

    // PUT: api/CommunityPost/5
    // 加 [Authorize]：改貼文內容／狀態／圖片／標籤是寫入動作，一定要登入才能做。
    [HttpPut("{communitypostid}")]
    [Authorize]
    public async Task<ResultDTO> PutCommunityPost(int? communitypostid, CommunityPostDTO communitypostDTO)
    {
        if (communitypostid != communitypostDTO.CommunityPostId)
        {
            return new ResultDTO { OK = false, Code = 400 };
        }

        CommunityPost post = await _context.CommunityPost.FindAsync(communitypostDTO.CommunityPostId);
        if (post == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        else
        {
            post.Content = communitypostDTO.Content;
            post.Status = communitypostDTO.Status;
            _context.Entry(post).State = EntityState.Modified;

            var oldImages = _context.PostImage.Where(i => i.CommunityPostId == post.CommunityPostId);
            _context.PostImage.RemoveRange(oldImages);

            if (communitypostDTO.Images != null)
            {
                foreach (var img in communitypostDTO.Images)
                {
                    PostImage postImage = new PostImage
                    {
                        PostImageId = 0,
                        CommunityPostId = post.CommunityPostId,
                        ImageFileName = img.ImageFileName,
                        SortOrder = img.SortOrder
                    };
                    _context.PostImage.Add(postImage);
                }
            }

            var oldTags = _context.PostTaggedProduct.Where(t => t.CommunityPostId == post.CommunityPostId);
            _context.PostTaggedProduct.RemoveRange(oldTags);

            if (communitypostDTO.TaggedProducts != null)
            {
                foreach (var tag in communitypostDTO.TaggedProducts)
                {
                    PostTaggedProduct postTaggedProduct = new PostTaggedProduct
                    {
                        PostTaggedProductId = 0,
                        CommunityPostId = post.CommunityPostId,
                        ProductId = tag.ProductId,
                        ProductRoute = tag.ProductRoute
                    };
                    _context.PostTaggedProduct.Add(postTaggedProduct);
                }
            }

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

    // POST: api/CommunityPost
    // 加 [Authorize]：發文一定要登入。
    [HttpPost]
    [Authorize]
    public async Task<ResultDTO> PostCommunityPost(CommunityPostDTO communitypostDTO)
    {
        CommunityPost post = new CommunityPost
        {
            CommunityPostId = 0,
            UserId = communitypostDTO.UserId,
            Content = communitypostDTO.Content,
            PostDate = DateTimeOffset.Now,
            Status = communitypostDTO.Status
        };
        _context.CommunityPost.Add(post);
        await _context.SaveChangesAsync();

        if (communitypostDTO.Images != null)
        {
            foreach (var img in communitypostDTO.Images)
            {
                PostImage postImage = new PostImage
                {
                    PostImageId = 0,
                    CommunityPostId = post.CommunityPostId,
                    ImageFileName = img.ImageFileName,
                    SortOrder = img.SortOrder
                };
                _context.PostImage.Add(postImage);
            }
        }

        if (communitypostDTO.TaggedProducts != null)
        {
            foreach (var tag in communitypostDTO.TaggedProducts)
            {
                PostTaggedProduct postTaggedProduct = new PostTaggedProduct
                {
                    PostTaggedProductId = 0,
                    CommunityPostId = post.CommunityPostId,
                    ProductId = tag.ProductId,
                    ProductRoute = tag.ProductRoute
                };
                _context.PostTaggedProduct.Add(postTaggedProduct);
            }
        }

        await _context.SaveChangesAsync();

        return new ResultDTO { OK = true, Code = 204 };
    }

    // DELETE: api/CommunityPost/5
    // 加 [Authorize]：刪貼文一定要登入。
    [HttpDelete("{communitypostid}")]
    [Authorize]
    public async Task<ResultDTO> DeleteCommunityPost(int? communitypostid)
    {
        var post = await _context.CommunityPost.FindAsync(communitypostid);
        if (post == null)
        {
            return new ResultDTO { OK = false, Code = 404 };
        }
        try
        {
            _context.CommunityPost.Remove(post);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return new ResultDTO { OK = false, Code = 500 };
        }
        return new ResultDTO { OK = true, Code = 204 };
    }
}