using MustRead.App.Forms;
using MustRead.App.Models;
using MustRead.App.Services;
using MustRead.Tests.Services;
using Xunit;

namespace MustRead.Tests.Forms;

public class AlertFormTests
{
    private static IEnumerable<Control> AllControls(Control root)
        => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));

    /// <summary>WinForms controls are created on an STA thread, as in the app.</summary>
    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw new Xunit.Sdk.XunitException(error.ToString());
    }

    [Fact]
    public void Labels_ShowAmpersandLiterally()
    {
        // Regression: Label.UseMnemonic defaults to true, so "R&D" was drawn as "RD".
        var dir = Path.Combine(Path.GetTempPath(), $"MustReadAlertForm_{Guid.NewGuid():N}");
        var settings = new TestableSettingsService(dir);
        var receiver = new MessageReceiverService(settings, new SessionInfoService(), new HistoryService(settings));
        var msg = new AlertMessage
        {
            MessageId = AlertMessage.GenerateId(),
            Title = "Avviso R&D",
            Body = "Riunione Sales & Marketing",
            SenderDomain = "DOM",
            SenderUsername = "a&b"
        };

        RunSta(() =>
        {
            using var form = new AlertForm(msg, receiver);
            var labels = AllControls(form).OfType<Label>().Where(l => l.Text.Contains('&')).ToList();

            Assert.Equal(3, labels.Count); // title, body, sender
            Assert.All(labels, l => Assert.False(l.UseMnemonic, $"UseMnemonic attivo su '{l.Text}'"));
        });
    }
}
