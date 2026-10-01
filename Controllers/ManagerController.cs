using Microsoft.AspNetCore.Mvc;
using DentalClinic.Infrastructure;
using DentalClinic.Models;

namespace DentalClinic.Controllers
{
    [Route("manager")]
    [RoleRequired(RoleIds.Manager)]
    public class ManagerController : BaseController
    {
        public ManagerController(DatabaseContext context) : base(context) { }

        [HttpGet("reviews/all")]
        public IActionResult Index()
        {
            var reviews = _context.Reviews.Where(r => r.IsVisible).OrderByDescending(r => r.CreatedAt).ToList();
            ViewBag.PendingCount = _context.Reviews.Count(r => !r.IsVisible);
            return View(ToCards(reviews));
        }

        [HttpGet("reviews/hidden")]
        public IActionResult HiddenReviews()
        {
            var reviews = _context.Reviews.Where(r => !r.IsVisible).OrderByDescending(r => r.CreatedAt).ToList();
            ViewBag.PendingCount = reviews.Count;
            return View(ToCards(reviews));
        }

        [HttpPost("reviews/{reviewId:long}/show")]
        public IActionResult Show(long reviewId)
        {
            var review = _context.Reviews.FirstOrDefault(r => r.Id == reviewId);
            if (review == null) return NotFound();

            review.IsVisible = true;
            _context.SaveChanges();

            TempData["Success"] = "Отзыв опубликован на сайте.";
            return RedirectToAction("HiddenReviews");
        }

        [HttpPost("reviews/{reviewId:long}/hide")]
        public IActionResult Hide(long reviewId)
        {
            var review = _context.Reviews.FirstOrDefault(r => r.Id == reviewId);
            if (review == null) return NotFound();

            review.IsVisible = false;
            _context.SaveChanges();

            TempData["Success"] = "Отзыв скрыт с сайта.";
            return RedirectToAction("Index");
        }
    }
}
