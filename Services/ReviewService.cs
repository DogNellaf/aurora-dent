using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services
{
    public enum ReviewSubmitStatus { Saved, NoFinishedVisit }

    public interface IReviewService
    {
        /// <summary>Creates or updates the patient's only review. Every change goes back to moderation.</summary>
        Task<ReviewSubmitStatus> SubmitAsync(Profile client, string text, int rating);
        Task<bool> HideOwnAsync(long profileId);
        Task<bool> SetVisibleAsync(long reviewId, bool visible);
        Task<bool> UpdateTextAsync(long reviewId, string text);
        Task<bool> DeleteAsync(long reviewId);
    }

    public sealed class ReviewService : IReviewService
    {
        private readonly DatabaseContext _db;
        private readonly IClinicClock _clock;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(DatabaseContext db, IClinicClock clock, ILogger<ReviewService> logger)
        {
            _db = db;
            _clock = clock;
            _logger = logger;
        }

        public async Task<ReviewSubmitStatus> SubmitAsync(Profile client, string text, int rating)
        {
            var now = _clock.Now;
            if (!await _db.Appointments.AnyAsync(a => a.ClientId == client.Id && a.StartAt < now))
                return ReviewSubmitStatus.NoFinishedVisit;

            rating = Math.Clamp(rating, 1, 5);
            var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ProfileId == client.Id);
            if (review is null)
            {
                var created = new Review { ProfileId = client.Id, Text = text, Rating = rating, IsVisible = false, CreatedAt = now };
                _db.Reviews.Add(created);

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // A double click or a second tab inserted the patient's review first (unique index on the
                    // profile). The later submit simply becomes an update of that review.
                    _db.Entry(created).State = EntityState.Detached;
                    review = await _db.Reviews.FirstAsync(r => r.ProfileId == client.Id);
                }
            }

            if (review is not null)
            {
                review.Text = text;
                review.Rating = rating;
                review.IsVisible = false;
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Profile {ProfileId} submitted a review for moderation", client.Id);
            return ReviewSubmitStatus.Saved;
        }

        public async Task<bool> HideOwnAsync(long profileId)
        {
            var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ProfileId == profileId);
            if (review is null) return false;

            review.IsVisible = false;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetVisibleAsync(long reviewId, bool visible)
        {
            var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review is null) return false;

            review.IsVisible = visible;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Review {ReviewId} {Action}", reviewId, visible ? "published" : "hidden");
            return true;
        }

        public async Task<bool> UpdateTextAsync(long reviewId, string text)
        {
            var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review is null) return false;

            review.Text = text;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(long reviewId)
        {
            var deleted = await _db.Reviews.Where(r => r.Id == reviewId).ExecuteDeleteAsync();
            return deleted > 0;
        }
    }
}
