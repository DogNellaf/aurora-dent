using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Infrastructure;
using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Controllers
{
    [Route("manager")]
    [RoleRequired(RoleIds.Manager)]
    public class ManagerController : BaseController
    {
        private readonly IReviewService _reviews;

        public ManagerController(DatabaseContext context, IReviewService reviews) : base(context)
        {
            _reviews = reviews;
        }

        [HttpGet("reviews/all")]
        public async Task<IActionResult> Index()
        {
            var reviews = await _context.Reviews.Where(r => r.IsVisible).OrderByDescending(r => r.CreatedAt).ToListAsync();
            ViewBag.PendingCount = await _context.Reviews.CountAsync(r => !r.IsVisible);
            return View(await ToCardsAsync(reviews));
        }

        [HttpGet("reviews/hidden")]
        public async Task<IActionResult> HiddenReviews()
        {
            var reviews = await _context.Reviews.Where(r => !r.IsVisible).OrderByDescending(r => r.CreatedAt).ToListAsync();
            ViewBag.PendingCount = reviews.Count;
            return View(await ToCardsAsync(reviews));
        }

        [HttpPost("reviews/{reviewId:long}/show")]
        public async Task<IActionResult> Show(long reviewId)
        {
            if (!await _reviews.SetVisibleAsync(reviewId, true)) return NotFound();

            TempData["Success"] = "Отзыв опубликован на сайте.";
            return RedirectToAction("HiddenReviews");
        }

        [HttpPost("reviews/{reviewId:long}/hide")]
        public async Task<IActionResult> Hide(long reviewId)
        {
            if (!await _reviews.SetVisibleAsync(reviewId, false)) return NotFound();

            TempData["Success"] = "Отзыв скрыт с сайта.";
            return RedirectToAction("Index");
        }
    }
}
