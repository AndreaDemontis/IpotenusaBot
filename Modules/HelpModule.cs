using System.Data;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Ipotenusa.Modules
{
	[Name("Aiuto")]
	[Summary("Questo aiuto vi aiuta tanto ad aiutare come aiutare i comandi\ndi questo aiuto.")]
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;
        private readonly IConfigurationRoot _config;

        public HelpModule(CommandService service, IConfigurationRoot config)
        {
            _service = service;
            _config = config;
        }

        [Command("help")]
		[Summary("Vi aiuta tanto.")]
        public async Task HelpAsync()
        {
            string prefix = _config["prefix"];

			foreach (var module in _service.Modules)
			{
				string description = null;
				DataTable commands = new DataTable();

				var builder = new EmbedBuilder()
				{
					Color = new Color(114, 137, 218)
				};

				commands.Columns.Add(new DataColumn("Command"));
				commands.Columns.Add(new DataColumn("Syntax"));
				commands.Columns.Add(new DataColumn("Description"));

				foreach (var cmd in module.Commands)
				{
					var result = await cmd.CheckPreconditionsAsync(Context);
					if (result.IsSuccess)
					{
						commands.Rows.Add(cmd.Name, prefix + cmd.Aliases.First(), cmd.Summary);
					}
				}

				string moduleDescription = "*" + module.Name + "*\n\n" + module.Summary + "\n\n";

				description = moduleDescription + Utils.Formatting.MakeTable(commands) + "\n" + Utils.Formatting.Separator(75, false);

				builder.Description = description;

				await ReplyAsync("", false, builder.Build());
			}
			
        }

        [Command("command")]
		[Summary("Vi aiuta poco.")]
		public async Task HelpAsync(string command)
        {
            var result = _service.Search(Context, command);

			var builder = new EmbedBuilder()
			{
				Color = new Color(114, 137, 218)
			};

			if (!result.IsSuccess)
            {
				builder.Color = Color.Red;
				builder.Description = $"Sorry, I couldn't find a command like **{command}**.";

				await ReplyAsync("", false, builder.Build());
                return;
            }

            string prefix = _config["prefix"];

            foreach (var match in result.Commands)
            {
                var cmd = match.Command;

				string desc = "";
				desc += "```\n";
				desc += "Alias:       " + string.Join(", ", cmd.Aliases.Select(x => prefix + x)) + "\n";
				desc += "Parameters:  " + (cmd.Parameters.Count == 0 ? "no parameters." : string.Join(", ", cmd.Parameters.Select(p => p.Name))) + "\n\n";
				desc += "Description: " + cmd.Summary;
				desc += "\n```";

				builder.AddField(x =>
                {
					x.Name = cmd.Name;
                    x.Value = desc;
                    x.IsInline = false;
                });
            }

            await ReplyAsync("", false, builder.Build());
        }
    }
}
