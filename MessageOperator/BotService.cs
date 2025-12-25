using AIMLbot;
namespace MessageOperator;

public class BotService
{
    private readonly Bot _bot;
    private Dictionary<string, User> _users;

    public BotService()
    {
        _bot = new Bot();
        _users =  new Dictionary<string, User>();
    }

    public string Talk(string input)
    {
        return "fuck yourself";
    }
}