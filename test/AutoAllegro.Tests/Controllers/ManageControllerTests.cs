using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoAllegro.Controllers;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Models.ManageViewModels;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AutoAllegro.Tests.Controllers
{
    public sealed class ManageControllerTests : DatabaseMock
    {
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _db;
        private readonly ManageController _controller;
        private readonly IServiceScope _scope;

        public ManageControllerTests()
        {
            _scope = CreateScope();
            _mapper = _scope.ServiceProvider.GetRequiredService<IMapper>();
            _db = GetDatabase(_scope);
            var userManager = GetUserManager(_scope);
            var signInManager = _scope.ServiceProvider.GetService<SignInManager<User>>();
            var logger = _scope.ServiceProvider.GetService<ILogger<ManageController>>();

            CreateFakeData();
            _controller = new ManageController(_mapper, _db, userManager, signInManager, logger);
        }

        [Fact]
        public async Task VirtualItemSettings_ReturnsCurrentUserSettings()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.VirtualItemSettings();

            // assert
            Assert.IsType<ViewResult>(result);
            Assert.IsType<VirtualItemSettingsViewModel>(((ViewResult)result).Model);

            var model = (VirtualItemSettingsViewModel)((ViewResult)result).Model;
            Assert.Equal("x", model.DisplayName);
            Assert.Equal("xy", model.MessageSubject);
            Assert.Equal("xyz\r\n\r\ntest\r\n", model.MessageTemplate);
            Assert.Equal("xyzx", model.ReplyTo);
        }

        [Fact]
        public async Task VirtualItemSettings_SavesVirtualSettings()
        {
            // arrange
            PopulateHttpContext(UserId2);

            // act
            IActionResult result = await _controller.VirtualItemSettings(new VirtualItemSettingsViewModel
            {
                MessageTemplate = "messageTemplate\r\n\r\ntest2\r\n",
                MessageSubject = "messageSubject",
                ReplyTo = "replyTo",
                DisplayName = "displayName"
            });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal(ManageMessageId.ChangedVirtualItemSettings, redirect.RouteValues["Message"]);

            var user = _db.Users.Include(t => t.VirtualItemSettings).Single(t => t.Id == UserId2);
            Assert.NotNull(user.VirtualItemSettings);
            Assert.Equal("displayName", user.VirtualItemSettings.DisplayName);
            Assert.Equal("messageSubject", user.VirtualItemSettings.MessageSubject);
            Assert.Equal("messageTemplate<br><br>test2<br>", user.VirtualItemSettings.MessageTemplate);
            Assert.Equal("replyTo", user.VirtualItemSettings.ReplyTo);
        }
        [Fact]
        public async Task VirtualItemSettings_UpdatesVirtualSettings()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.VirtualItemSettings(new VirtualItemSettingsViewModel
            {
                MessageTemplate = "messageTemplate",
                MessageSubject = "messageSubject",
                ReplyTo = "replyTo",
                DisplayName = "displayName"
            });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal(ManageMessageId.ChangedVirtualItemSettings, redirect.RouteValues["Message"]);

            var user = _db.Users.Include(t => t.VirtualItemSettings).Single(t => t.Id == UserId);
            Assert.NotNull(user.VirtualItemSettings);
            Assert.Equal("displayName", user.VirtualItemSettings.DisplayName);
            Assert.Equal("messageSubject", user.VirtualItemSettings.MessageSubject);
            Assert.Equal("messageTemplate", user.VirtualItemSettings.MessageTemplate);
            Assert.Equal("replyTo", user.VirtualItemSettings.ReplyTo);
        }
        private void PopulateHttpContext(string userId)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var httpContext = _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            _controller.ControllerContext.HttpContext = httpContext;
        }
    }
}
