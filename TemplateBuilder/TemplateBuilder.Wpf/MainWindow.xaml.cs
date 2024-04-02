using TemplateBuilder.Wpf.CommandDefinitions;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Serialization;

namespace TemplateBuilder.Wpf
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			Commands = new();
			try
			{
				LoadCommands();
			}
			catch
			{
				MessageBox.Show("Unable to load xml.");
			}
		}

		CommandDefinitions.CommandDefinitions Commands { get; set; }

		CommandDefinitions.CommandDefinitionsCommandGroupCommand? CurrentCommand { get; set; }

		public void LoadCommands()
		{
			var xs = new XmlSerializer(typeof(CommandDefinitions.CommandDefinitions));
			using var sr = new FileStream(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location) ?? string.Empty, "Templates/Template.xml"), FileMode.Open);
			Commands = (xs.Deserialize(sr) as CommandDefinitions.CommandDefinitions) ?? throw new FileFormatException();

			foreach (var group in Commands.Commands)
			{
				if (group is null) continue;
				UIElementCollection children = PanelCommands.Children;
				if (group.Items.Length > 1)
				{
					var panel = new StackPanel() { Orientation = Orientation.Horizontal };
					var gb = new GroupBox() { Header = group.title, Content = panel };
					children = panel.Children;
					PanelCommands.Children.Add(gb);
				}
				foreach (var command in group.Items)
				{
					if (command is null) continue;
					var button = new Button()
					{
						Content = command.title,
						Tag = command,
					};
					button.Click += Button_Click_CommandSelect;
					children.Add(button);
				}
			}
		}

		void InsertText(string text)
		{
			TextBoxInput.SelectedText = text;
			TextBoxInput.SelectionStart += TextBoxInput.SelectedText.Length;
			TextBoxInput.SelectionLength = 0;

		}

		private void Button_Click_CommandSelect(object sender, RoutedEventArgs e)
		{
			if (sender is not Button button) return;
			var command = CurrentCommand = button.Tag as CommandDefinitions.CommandDefinitionsCommandGroupCommand;
			PanelArgs.Children.Clear();
			if (command?.Modifiers is null || command.Modifiers.Length == 0)
			{
				InsertText((command?.command ?? string.Empty) + "\n");
				return;
			}
			int i = 0;
			foreach (var modifier in command.Modifiers)
			{
				i++;
				UIElementCollection children = PanelArgs.Children;

				var group = Commands?.ModifierDefinitions?.FirstOrDefault(a => a.key == modifier.group);
				if (group?.Items is null) continue;

				if (command.Modifiers.Length > 1)
				{
					var panel = new StackPanel() { Orientation = Orientation.Horizontal };
					var gb = new GroupBox() { Header = string.IsNullOrEmpty(group.title) ? modifier.group : group.title, Content = panel };
					children = panel.Children;
					PanelArgs.Children.Add(gb);
				}

				foreach (var item in group.Items)
				{
					var toggleButton = new ToggleButton()
					{
						Content = string.IsNullOrEmpty(item.title) ? item.arg : item.title,
						Tag = item,
					};
					toggleButton.Checked += ToggleButton_Checked;
					if (item.arg == modifier.defaultArg) toggleButton.IsChecked = true;
					children.Add(toggleButton);
				}
			}
		}

		private void ToggleButton_Checked(object sender, RoutedEventArgs e)
		{
			if (PanelArgs.Children.Count == 0) return;
			if ((sender as ToggleButton)?.Tag is not CommandDefinitions.CommandDefinitionsModifierGroupModifierDefinition tag) return;
			if (PanelArgs.Children[0] is ToggleButton)
			{
				//TextBoxInput.SelectedText = command?.Content ?? string.Empty;
				//TextBoxInput.SelectionLength = 0;
				InsertText($"{CurrentCommand?.command} {tag.arg}\n");
				PanelArgs.Children.Clear();
				return;
			}
			var commands = new List<string>() { CurrentCommand?.command ?? string.Empty };

			bool isAllSelected = true;
			foreach (var item in PanelArgs.Children)
			{
				if ((item as GroupBox)?.Content is not StackPanel stackPanel) return;
				ToggleButton? selectedButton = null;
				bool groupContainsSender = false;
				foreach (var child in stackPanel.Children)
				{
					if (child == sender) groupContainsSender = true;
					if (child is not ToggleButton toggle) continue;
					if (toggle.IsChecked == true) selectedButton = toggle;

				}
				if (groupContainsSender)
				{
					foreach (var child in stackPanel.Children)
					{
						if (child is not ToggleButton toggle) continue;
						if (toggle.IsChecked == true && toggle != sender) toggle.IsChecked = false;
					}
				}
				if (selectedButton == null) isAllSelected = false;
				else commands.Add((selectedButton.Tag as CommandDefinitions.CommandDefinitionsModifierGroupModifierDefinition)?.arg ?? string.Empty);
			}
			if (isAllSelected)
			{
				InsertText($"{string.Join(' ', commands)}\n");
				PanelArgs.Children.Clear();
				return;
			}
		}

		public void ExecuteConversion()
		{
			var sr = new StringReader(TextBoxInput.Text);
			var sb = new StringBuilder();
			while (true)
			{
				var line = sr.ReadLine();
				if (line is null) break;
				if (string.IsNullOrWhiteSpace(line)) continue;
				if (line.StartsWith("#"))
				{
					sb.AppendLine(line);
					continue;
				}
				var texts = line.Split(' ');
				var command = Commands.Commands.SelectMany(a => a.Items).FirstOrDefault(a => a.command.Equals(texts[0], StringComparison.InvariantCultureIgnoreCase));
				if (command is null)
				{
					sb.AppendLine($"# Invalid command : {line}");
					continue;
				}
				var text = command.Content;
				if (command.Modifiers is not null)
				{
					int i = 0;
					foreach (var item in command.Modifiers)
					{
						i++;
						var arg = texts.Length > i ? texts[i] : item.defaultArg;
						var mgroup = Commands.ModifierDefinitions.FirstOrDefault(a => a.key == item.group);
						if (mgroup?.Items is null) continue;
						var mitem = mgroup.Items.FirstOrDefault(a => a.arg == arg);
						if (mitem is null) continue;
						var replaceWith = string.IsNullOrWhiteSpace(item.replaceWithOverride) ? mgroup.repleceWith : item.replaceWithOverride;
						text = text.Replace($"{{{replaceWith}}}", mitem.Value);
					}
				}
				sb.AppendLine(text);
			}
			TextBoxResult.Text = sb.ToString();
		}

		int lastInputLineCount = -1;

		private void TextBoxInput_TextChanged(object sender, TextChangedEventArgs e)
		{
			var text = (sender as TextBox)?.Text;
			if (text is null) return;
			var lineCount = text.Count(a => a == '\n');
			if (lineCount != lastInputLineCount) ExecuteConversion();
			lastInputLineCount = lineCount;
		}
	}
}