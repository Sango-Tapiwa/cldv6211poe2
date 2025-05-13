using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventEase.Models;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;

namespace EventEase.Controllers
{
    public class VenuesController : Controller

    {
        private async Task<string> UploadImageToBlobAsync(IFormFile imageFile)
        {
            


            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(Guid.NewGuid() + Path.GetExtension(imageFile.FileName));

            var blobHttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = imageFile.ContentType
            };

            using (var stream = imageFile.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders
                });
            }

            return blobClient.Uri.ToString();
        }



        private readonly ApplicationDbContext _context;

        public VenuesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var venues = await _context.Venues.ToListAsync();
            return View(venues);
        }

        public IActionResult Create()
        {
            return View();
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Venues venue)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (venue.ImageFile != null)
                    {
                        try
                        {
                            var blobUrl = await UploadImageToBlobAsync(venue.ImageFile);
                            venue.ImageURL = blobUrl;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error uploading image: {ex.Message}");
                            TempData["ErrorMessage"] = $"Error uploading image: {ex.Message}";
                            return View(venue);
                        }
                    }

                    _context.Add(venue);
                    await _context.SaveChangesAsync();
                    Console.WriteLine("Venue created successfully.");
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    Console.WriteLine("Model state is invalid.");
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        Console.WriteLine($"Model error: {error.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating venue: {ex.Message}");
                TempData["ErrorMessage"] = $"Error creating venue: {ex.Message}";
            }

            return View(venue);
        }



        // This method will return a form to delete a venue
        public async Task<IActionResult> Delete(int id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var venue = await _context.Venues
                .FirstOrDefaultAsync(v => v.VenueID == id);
            if (venue == null)
            {
                return NotFound();
            }
            return View(venue);
        }

        // This method will handle the deletion of the venue
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var venue = await _context.Venues.FindAsync(id);

            // Check for related events with bookings
            bool hasBookings = await _context.Events
                .AnyAsync(e => e.VenueID == id && _context.Bookings.Any(b => b.EventID == e.EventID));

            if (hasBookings)
            {
                TempData["Error"] = "Cannot delete this venue because it has active bookings.";
                return RedirectToAction(nameof(Index));
            }

            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Venues/Edit/{id}
        public async Task<IActionResult> Edit(int? id)
        {
            Console.WriteLine($"GET Edit method called with ID: {id}");

            if (id == null)
            {
                Console.WriteLine("ID is null, returning NotFound.");
                return NotFound();
            }

            var venue = await _context.Venues.FindAsync(id);

            if (venue == null)
            {
                Console.WriteLine($"Venue with ID {id} not found.");
                return NotFound();
            }

            Console.WriteLine($"Venue with ID {id} found. Returning Edit view.");
            return View(venue);
        }


        // POST: Venues/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Venues venue)
        {
            Console.WriteLine($"POST Edit method called with ID: {id}");

            if (id != venue.VenueID)
            {
                Console.WriteLine($"ID mismatch: Route ID ({id}) vs VenueID ({venue.VenueID})");
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    Console.WriteLine("Model state is valid. Proceeding with update...");

                    var existingVenue = await _context.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueID == id);

                    if (existingVenue == null)
                    {
                        Console.WriteLine($"Venue with ID {id} not found in the database.");
                        return NotFound();
                    }

                    // Check if ImageFile is properly received
                    if (venue.ImageFile != null)
                    {
                        Console.WriteLine($"Image file received. Name: {venue.ImageFile.FileName}, Size: {venue.ImageFile.Length} bytes");

                        try
                        {
                            var blobUrl = await UploadImageToBlobAsync(venue.ImageFile);
                            Console.WriteLine($"Image uploaded successfully. Blob URL: {blobUrl}");
                            venue.ImageURL = blobUrl;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Image upload failed: {ex.Message}");
                            TempData["ErrorMessage"] = $"Error uploading image: {ex.Message}";
                            return View(venue);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No image file detected.");
                        venue.ImageURL = existingVenue.ImageURL;
                    }

                    Console.WriteLine($"Updating Venue: ID: {venue.VenueID}, ImageURL: {venue.ImageURL}");

                    existingVenue.VenueName = venue.VenueName;
                    existingVenue.Location = venue.Location;
                    existingVenue.Capacity = venue.Capacity;
                    existingVenue.Description = venue.Description;
                    existingVenue.ImageURL = venue.ImageURL;

                    _context.Update(existingVenue);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Venue with ID {venue.VenueID} successfully updated.");

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error during update: {ex.Message}");
                    throw;
                }
            }

            Console.WriteLine("Model state is invalid. Returning to Edit view with validation errors.");
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
            {
                Console.WriteLine($"Model error: {error.ErrorMessage}");
            }

            return View(venue);
        }






        // This method will return the details of a venue
        // '?' means that the parameter is nullable, which means it can be null or have a value of type int.

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();  // it will return a 404 Not Found error if the id is null
            }

            //This will  find the venue with the specified id and include the events associated with the venue
            var venue = await _context.Venues
                .Include(v => v.Events)
                .FirstOrDefaultAsync(v => v.VenueID == id);

            if (venue == null)
            {
                return NotFound();  // it will return a 404 Not Found error if the venue is null
            }

            return View(venue);
        }

        

        // Check if the venue exists in the database
        private bool VenueExists(int id)
        {
            return _context.Venues.Any(e => e.VenueID == id);
        }
    }

}
