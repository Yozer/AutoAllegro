using System;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AutoAllegro.Models;
using AutoAllegro.Models.ManageViewModels;
using Microsoft.EntityFrameworkCore;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<ManageController> _logger;

        public ManageController(
        ApplicationDbContext dbContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<ManageController> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        //
        // GET: /Manage/Index
        [HttpGet]
        public async Task<IActionResult> Index(ManageMessageId? message = null)
        {
            ViewData["StatusMessage"] =
                message == ManageMessageId.ChangePasswordSuccess ? "Twoje hasło zostało zmienione."
                : message == ManageMessageId.ChangeAllegroSettings ? "Dane dostępowe do Allegro zostały zmienione pomyślnie."
                : message == ManageMessageId.Error ? "Wystąpił błąd."
                : string.Empty;

            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return View("Error");
            }
            var model = new IndexViewModel();
            return View(model);
        }

        // GET: /Manage/AllegroSettings
        [HttpGet]
        public async Task<IActionResult> AllegroSettings() 
        {
            string userId = _userManager.GetUserId(User);
            var user = await _dbContext.Users.FirstOrDefaultAsync(t => t.Id == userId);
            if (user != null)
            {
                var model = new AllegroSettingsViewModel
                {
                    Login = user.AllegroUserName ?? string.Empty,
                    HashPassword = user.AllegroHashedPass ?? string.Empty,
                    ApiKey = user.AllegroKey ?? string.Empty
                };

                return View(model);
            }
            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
        }

        // POST: /Manage/AllegroSettings
        [HttpPost]
        public async Task<IActionResult> AllegroSettings(AllegroSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string userId = _userManager.GetUserId(User);
            var user = await _dbContext.Users.FirstOrDefaultAsync(t => t.Id == userId);
            if (user != null)
            {
                user.AllegroHashedPass = model.HashPassword;
                user.AllegroKey = model.ApiKey;
                user.AllegroUserName = model.Login;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("User changed allegro settings successfully.");
                return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangeAllegroSettings });
            }
            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
        }

        //
        // GET: /Manage/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    _logger.LogInformation(3, "User changed their password successfully.");
                    return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangePasswordSuccess });
                }
                AddErrors(result);
                return View(model);
            }
            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
        }


        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            Error,
            ChangeAllegroSettings
        }

        private Task<User> GetCurrentUserAsync()
        {
            return _userManager.GetUserAsync(HttpContext.User);
        }

        #endregion
    }
}
