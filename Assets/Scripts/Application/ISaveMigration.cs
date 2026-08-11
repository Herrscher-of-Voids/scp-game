using Newtonsoft.Json.Linq;

namespace Scp.Application
{
    public interface ISaveMigration
    {
        int FromVersion { get; }

        JObject Migrate(JObject save);
    }
}
