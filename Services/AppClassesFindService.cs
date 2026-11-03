using System.Reflection;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Services.Chat.Bots;

namespace TestingAppWeb.Services
{
    public static class AppClassesFindService
    {
        public static IEnumerable<Type> FindChatBotTypes()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes();

            return types
                .Where(t => !t.IsInterface && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Contains(typeof(IChatBot)))
                .ToList();
        }
    }
}