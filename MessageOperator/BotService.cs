using AIMLbot;
using System.IO;
using System.Collections.Generic;
using System;

namespace MessageOperator;

public class BotService
{
	private readonly Bot _bot;
	private Dictionary<long, User> _users;

	public BotService()
	{
		_bot = new Bot();
		_users = new Dictionary<long, User>();

		var baseDir = AppContext.BaseDirectory;
		
		_bot.UpdatedConfigDirectory = Path.Combine(baseDir, "config");
		_bot.loadSettings();
		
		_bot.UpdatedAimlDirectory = Path.Combine(baseDir, "aiml");
		_bot.isAcceptingUserInput = false;
		_bot.loadAIMLFromFiles(); 
		_bot.isAcceptingUserInput = true;
	}

	public void AddUser(long id)
	{
		if (!_users.ContainsKey(id))
		{
			User newUser = new User(id.ToString(), _bot);
			_users.Add(id, newUser);
		}
	}

	public string Talk(string input, long userId)
	{
		if (!_users.ContainsKey(userId))
		{
			AddUser(userId);
		}

		User user = _users[userId];
        
		Request request = new Request(input, user, _bot);
		Result result = _bot.Chat(request);

		return result.Output;
	}
    
	// Метод для связи с нейросетью (вызовите его, когда распознаете фото)
	public string HandleImageRecognition(string letterName, long userId)
	{
		// "RECOGNIZED" — это ключевое слово, которое мы добавили в AIML
		return Talk($"RECOGNIZED {letterName.ToLower()}", userId);
	}
}