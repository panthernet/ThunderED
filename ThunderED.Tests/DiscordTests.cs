using FluentAssertions;
using ThunderED.Tests.Fixtures;
using Xunit;

namespace ThunderED.Tests
{
    public class DiscordTests: IClassFixture<DiscordFixture>
    {
        private readonly DiscordFixture _fix;
        private const ulong TEST_CHANNEL = 542649172248231937;
        private const ulong TEST_GUILD = 424675065645236229;
        private const string TEST_GUILD_ADMIN_ROLE = "Admins";
        private const string TEST_GUILD_ROLE = "Test";
        private const ulong TEST_USER_ID = 207041545679929345;

        public DiscordTests(DiscordFixture fix)
        {
            _fix = fix;
        }

        [Fact]
        public async void GetGuildTest()
        {
            _fix.API.GetGuild(TEST_GUILD).Should().NotBeNull();
        }

        [Fact]
        public async void GetChannelTest()
        {
            _fix.API.GetChannel(TEST_CHANNEL).Should().NotBeNull();
            _fix.API.GetChannel(TEST_CHANNEL).Should().NotBeNull();
        }

        [Fact]
        public async void GetGuildRoleTest()
        {
            _fix.API.GetGuildRole(TEST_GUILD_ADMIN_ROLE).Should().NotBeNull();
        }

        [Fact]
        public async void GetUserTest()
        {
            var user = _fix.API.GetUser(TEST_USER_ID);
            user.Should().NotBeNull();
            var role = _fix.API.GetUserRole(user, TEST_GUILD_ADMIN_ROLE);
            role.Should().NotBeNull();
            _fix.API.GetUserRoleNames(TEST_USER_ID).Should().NotBeNullOrEmpty();
            _fix.API.GetRoleMention(TEST_GUILD_ROLE).Should().NotBeNullOrEmpty();
            (await _fix.API.GetUserMention(TEST_USER_ID)).Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async void SendMessageTest()
        {
            await _fix.API.SendMessageAsync(TEST_CHANNEL, "This is a test message!");
        }

        [Fact]
        public async void GetGroupTest()
        {
        }
    }
}
