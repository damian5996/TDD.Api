using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace TDD.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void ShouldLoginWithSuccess()
        {
            var user = new User {Username = "damian", Email = "damian@wp.pl", Password = GetHash("qwerty") };

            var loginModel = new LoginModel
            {
                Username = "damian",
                Password = "qwerty"
            };

            var userRepository = new Mock<IRepository<User>>();
            userRepository.Setup(x => x.Exist(It.IsAny<Func<User, bool>>())).Returns(true);
            userRepository.Setup(x => x.GetBy(It.IsAny<Func<User, bool>>())).Returns(user);

            var configurationMock = new Mock<IConfigurationManager>();
            configurationMock.Setup(x => x.GetValue(It.IsAny<string>())).Returns("qwertyuiosdfghjkkjhgfdsdfghjklhgfd");

            var userService = new UserService(userRepository.Object, configurationMock.Object);

            var accountController = new AccountController(userService);

            var result = accountController.Login(loginModel);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var email = Assert.IsAssignableFrom<ResultDto<LoginResultDto>>(okResult.Value);

            Assert.NotNull(email.SuccessResult?.Token);

        }

        [Fact]
        public void ShouldReturnErrorOnInvalidUser()
        {
            var error = "Has³o lub u¿ytkownik b³êdne";

            var loginModel = new LoginModel
            {
                Username = "damian",
                Password = "qwerty"
            };

            var userRepository = new Mock<IRepository<User>>();
            var configurationMock = new Mock<IConfigurationManager>();
            configurationMock.Setup(x => x.GetValue(It.IsAny<string>())).Returns("qwertyuiosdfghjkkjhgfdsdfghjklhgfd");

            var userService = new UserService(userRepository.Object, configurationMock.Object);
            var accountController = new AccountController(userService);

            var result = accountController.Login(loginModel);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var errorResult = Assert.IsAssignableFrom<ResultDto<LoginResultDto>>(badRequest.Value);

            Assert.Contains(error, errorResult.Errors);

        }

        [Fact]
        public void ShouldReturnErrorOnInvalidPassword()
        {
            var error = "Has³o lub u¿ytkownik b³êdne";
            var user = new User {Username = "damian", Email = "damian@wp.pl", Password = GetHash("qwerty")};
            var loginModel = new LoginModel
            {
                Username = "damian",
                Password = "qwe"
            };

            var userRepository = new Mock<IRepository<User>>();
            userRepository.Setup(x => x.Exist(It.IsAny<Func<User, bool>>())).Returns(true);
            userRepository.Setup(x => x.GetBy(It.IsAny<Func<User, bool>>())).Returns(user);

            var configurationMock = new Mock<IConfigurationManager>();
            configurationMock.Setup(x => x.GetValue(It.IsAny<string>())).Returns("qwertyuiosdfghjkkjhgfdsdfghjklhgfd");

            var userService = new UserService(userRepository.Object, configurationMock.Object);
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
        private string GetHash(string text)
        {
            // SHA512 is disposable by inheritance.  
            using (var sha256 = SHA256.Create())
            {
                // Send a sample text to hash.  
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                // Get the hashed string.  
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }

        public class UserService : IUserService
        {
            private readonly IRepository<User> _userRepository;
            private readonly IConfigurationManager _configuration;

            public UserService(IRepository<User> userRepository, IConfigurationManager configuration)
            {
                _userRepository = userRepository;
                _configuration = configuration;
            }
            public ResultDto<LoginResultDto> Login(LoginModel loginModel)
            {
                var result = new ResultDto<LoginResultDto>
                {
                    Errors = new List<string>()
                };
                
                var user = _userRepository.GetBy(x => x.Username == loginModel.Username);

                
                if (user?.Password != GetHash(loginModel.Password))
                {
                    result.Errors.Add("Has³o lub u¿ytkownik b³êdne");
                    return result;
                }
                var token = BuildToken(user, _configuration.GetValue("Jwt:Key"),
                    _configuration.GetValue("Jwt: Issuer"));

                result.SuccessResult = new LoginResultDto {Token = token};
                return result;
            }
            private string GetHash(string text)
            {
                // SHA512 is disposable by inheritance.  
                using (var sha256 = SHA256.Create())
                {
                    // Send a sample text to hash.  
                    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                    // Get the hashed string.  
                    return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
                }
            }
            public string BuildToken(User user, string secretKey, string issuer, DateTime? expirationDate = null)
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var claims = new[]
                {
                    new Claim(ClaimTypes.GivenName, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Sid, user.Id.ToString())
                };
                var token = new JwtSecurityToken(issuer, issuer, claims, expires: expirationDate,
                    signingCredentials: creds);

                return new JwtSecurityTokenHandler().WriteToken(token);
            }

        }
        public class LoginResultDto : BaseDto
        {
            public string Token { get; set; }
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
            public long Id { get; set; }
        }
        public interface IConfigurationManager
        {
            string GetValue(string key);
        }

        public class ConfigurationManager : IConfigurationManager
        {
            private readonly IConfiguration _configuration;

            public ConfigurationManager(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            public string GetValue(string key)
            {
                return _configuration[key];
            }
        }
    }
    
}
