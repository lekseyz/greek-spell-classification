using AIMLbot;
namespace MessageOperator;

public class BotService
{
    private readonly Bot _bot;
    private Dictionary<long, User> _users;

    public BotService()
    {
        _bot = new Bot();
        _users =  new Dictionary<long, User>();
    }

    public string Talk(string input, long userId)
    {
        return "fuck yourself";
    }

    public void AddUser(long id)
    {
        
    }
}