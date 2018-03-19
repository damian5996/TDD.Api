using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;

namespace TDD.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void ShouldLoginWithSuccess()
        {
            var user = new User {Username = "damian", Email = "damian@wp.pl", Password = "qwerty"};

            var loginModel = new LoginModel
            {
                Username = "damian",
                Password = "qwerty"
            };

            var userRepository = new Mock<IRepository<User>>();
            userRepository.Setup(x => x.Exist(It.IsAny<Func<User, bool>>())).Returns(true);
            userRepository.Setup(x => x.GetBy(It.IsAny<Func<User, bool>>())).Returns(user);

            var userService = new UserService(userRepository.Object);
            var accountController = new AccountController(userService);

            var result = accountController.Login(loginModel);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var email = Assert.IsAssignableFrom<ResultDto<LoginResultDto>>(okResult.Value);

            Assert.Equal(user.Email, email.SuccessResult.Email);

        }

        [Fact]
        public void ShouldReturnErrorOnInvalidUser()
        {
            var error = "Has�o lub u�ytkownik b��dne";

            var loginModel = new LoginModel
            {
                Username = "damian",
                Password = "qwerty"
            };

            var userRepository = new Mock<IRepository<User>>();
            var userService = new UserService(userRepository.Object);
            var accountController = new AccountController(userService);

            var result = accountController.Login(loginModel);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var errorResult = Assert.IsAssignableFrom<ResultDto<LoginResultDto>>(badRequest.Value);

            Assert.Contains(error, errorResult.Errors);

        }

        public class AccountController : Controller
        {
            private readonly IUserService _userService;

            public AccountController(IUserService userService)
            {
                _userService = userService;
            }

            public IActionResult Login(LoginModel loginModel)
            {
                var result = _userService.Login(loginModel);

                if (result.IsError)
                {
                    return BadRequest(result);
                }
                return Ok(result);
            }
        }

        public class UserService : IUserService
        {
            private readonly IRepository<User> _userRepository;

            public UserService(IRepository<User> userRepository)
            {
                _userRepository = userRepository;
            }
            public ResultDto<LoginResultDto> Login(LoginModel loginModel)
            {
                var result = new ResultDto<LoginResultDto>
                {
                    Errors = new List<string>()
                };

                var isUserExist = _userRepository.Exist(x => x.Username == loginModel.Username);

                var user = _userRepository.GetBy(x => x.Username == loginModel.Username);

                if (!isUserExist)
                {
                    result.Errors.Add("Has�o lub u�ytkownik b��dne");
                    return result;
                }
                

                result.SuccessResult = new LoginResultDto {Email = user.Email};
                return result;
            }
            
        }
        public class LoginResultDto : BaseDto
        {
            public string Email { get; set; }
        }

        public class ResultDto<T> where T : BaseDto
        {
            public T SuccessResult { get; set; }
            public List<string> Errors { get; set; }
            public bool IsError => Errors?.Count > 0;
        }

        public class BaseDto
        {
        }

        public interface IUserService
        {
            ResultDto<LoginResultDto> Login(LoginModel loginModel);
        }

        public interface IRepository<T> where T : Entity
        {
            bool Exist(Func<User, bool> function);
            T GetBy(Func<User, bool> function);
        }

        public class LoginModel
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class User : Entity
        {
            public string Username { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class Entity
        {
        }
    }
    
}
