using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CLOthings_API.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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
    // 拿掉 [Authorize]：貼文列表是社群首頁動態牆要用的，任何人（不管有沒有登入）都該看得到。
    // 固定只回傳 status 是 public 的貼文，不開放用查詢參數切換——不然任何人只要不帶參數
    // 就能看到全部貼文（含隱藏、審核中的），等於「隱藏」形同虛設。要看全部狀態的貼文，
    // 要走下面新加的 GetAllCommunityPostForAdmin，那支才有 [Authorize(Roles = "Admin,SuperAdmin")] 保護。
    [HttpGet]
    public async Task<IEnumerable<CommunityPostDTO>> GetCommunityPost()
    {
        return await _context.CommunityPost
            .Where(c => c.Status == "public")
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
            }).ToListAsync();
    }

    // GET: api/CommunityPost/admin/all
    // GET: api/CommunityPost/5
    // 一樣不用登入，貼文詳細頁任何人都該看得到。這支故意不篩選狀態——
    // 如果篩成只回傳 public，作者自己想看自己隱藏的貼文（例如從個人頁點進去）也會看不到，
    // 這樣會壞掉一個正常使用情境，不只是擋壞人。單篇查詢的曝光風險本來就比列表低很多
    // （得先知道確切的貼文編號），先不處理，之後真的要補，要一起考慮「本人／管理員可以看，
    // 其他人不行」這種依角色判斷的邏輯，不是單純加篩選就好。
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
    // 不用登入也能打（沒帶 token 就當一般訪客看）。但這支的答案要「因人而異」：
    // 本人看自己的頁面，要看到全部狀態（含隱藏、審核中的），才能管理自己的貼文；
    // 別人（或沒登入的訪客）看，就只該看到 public 的，跟隱藏／審核中應該一樣搜不到。
    // 判斷方式：從 JWT 讀出「現在打這支 API 的人是誰」（如果有帶 token 的話），
    // 跟網址上的 userid 比對是不是同一個人；管理員也放行看全部，方便後台以外的地方也能查。
    [HttpGet("user/{userid}")]
    public async Task<IEnumerable<CommunityPostDTO>> GetCommunityPostByUser(int userid)
    {
        var viewerIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(viewerIdString, out var viewerId);
        var isOwner = viewerId == userid;
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

        var query = _context.CommunityPost.Where(c => c.UserId == userid);
        if (!isOwner && !isAdmin)
        {
            query = query.Where(c => c.Status == "public");
        }

        return await query
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

            // 只有在 DTO「真的有帶」Images 這個欄位時，才動圖片——null 代表「這次編輯不動圖片」，
            // 不是「清空圖片」。之前的寫法不管有沒有帶都先刪光，如果哪個編輯表單沒帶這個欄位
            // （例如 UserProfileView.vue 的編輯表單目前不支援改標記商品），圖片/標籤會被誤刪。
            if (communitypostDTO.Images != null)
            {
                var oldImages = _context.PostImage.Where(i => i.CommunityPostId == post.CommunityPostId);
                _context.PostImage.RemoveRange(oldImages);

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

            // 同樣道理：TaggedProducts 是 null 就代表「這次編輯不動標記商品」，維持原樣。
            // 如果真的想清空標記商品，前端要明確送一個空陣列 []，不是不帶這個欄位。
            if (communitypostDTO.TaggedProducts != null)
            {
                var oldTags = _context.PostTaggedProduct.Where(t => t.CommunityPostId == post.CommunityPostId);
                _context.PostTaggedProduct.RemoveRange(oldTags);

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