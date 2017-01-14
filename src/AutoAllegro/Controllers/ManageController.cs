using System;
using System.Threading.Tasks;
using AutoAllegro.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AutoAllegro.Models;
using AutoAllegro.Models.ManageViewModels;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<ManageController> _logger;

        public ManageController(
        IMapper mapper,
        ApplicationDbContext dbContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<ManageController> logger)
        {
            _mapper = mapper;
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
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return View("Error");
            }
            var model = new IndexViewModel
            {
                Message = message
            };
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
                return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangedAllegroSettings });
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
        public async Task<IActionResult> VirtualItemSettings()
        {
            string userId = _userManager.GetUserId(User);
            var user = await _dbContext.Users.Include(t => t.VirtualItemSettings).FirstOrDefaultAsync(t => t.Id == userId);
            if (user != null)
            {
                var model = _mapper.Map<VirtualItemSettingsViewModel>(user.VirtualItemSettings);
                return View(model);
            }
            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
        }
        [HttpPost]
        public async Task<IActionResult> VirtualItemSettings(VirtualItemSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (string.IsNullOrEmpty(model.MessageSubject) ^ string.IsNullOrEmpty(model.MessageTemplate))
            {
                return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
            }

            string userId = _userManager.GetUserId(User);
            var user = await _dbContext.Users.Include(t => t.VirtualItemSettings).FirstAsync(t => t.Id == userId);
            user.VirtualItemSettings = _mapper.Map<VirtualItemSettings>(model);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User changed virtual item settings successfully.");
            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangedVirtualItemSettings });
        }

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private Task<User> GetCurrentUserAsync()
        {
            return _userManager.GetUserAsync(HttpContext.User);
        }

        #endregion
    }
}
