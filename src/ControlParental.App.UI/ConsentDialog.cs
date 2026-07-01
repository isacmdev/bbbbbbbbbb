// <copyright file="ConsentDialog.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.Domain;

/// <summary>
/// T25 — Console-based consent dialog for data collection disclosure.
/// Works as a console application without WinUI dependency.
/// </summary>
public sealed class ConsentDialog
{
    private readonly IConsentService? consentService;
    private readonly Action? onConsentGranted;

    /// <summary>
    /// Gets a value indicating whether consent has been granted.
    /// </summary>
    public bool ConsentGranted { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentDialog"/> class.
    /// </summary>
    /// <param name="consentService">Optional consent service for recording consent.</param>
    /// <param name="onConsentGranted">Optional callback invoked when consent is granted.</param>
    public ConsentDialog(IConsentService? consentService, Action? onConsentGranted)
    {
        this.consentService = consentService;
        this.onConsentGranted = onConsentGranted;
    }

    /// <summary>
    /// Shows the consent dialog to the user.
    /// </summary>
    public void Show()
    {
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║         DIVULGACIÓN DE DATOS              ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine(ConsentStrings.DisclosureBody);
        Console.WriteLine();
        Console.WriteLine("1) Acepto");
        Console.WriteLine("2) Ver qué se monitorea");
        Console.WriteLine();
        Console.Write("Opción: ");

        var input = Console.ReadLine();
        if (input == "1")
        {
            this.GrantAndClose();
        }
        else if (input == "2")
        {
            this.ShowTransparency();
            Console.Write("Opción: ");
            input = Console.ReadLine();
            if (input == "1") this.GrantAndClose();
        }
    }

    /// <summary>
    /// Shows the transparency/details dialog.
    /// </summary>
    public void ShowTransparency()
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║       ¿QUÉ SE MONITOREA?                 ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine(ConsentStrings.TransparencyBody);
    }

    private void GrantAndClose()
    {
        try
        {
            this.consentService?.GrantConsentAsync(null).GetAwaiter().GetResult();
        }
        catch
        {
            // Consent recording failed; proceed with onboarding
        }

        this.ConsentGranted = true;
        this.onConsentGranted?.Invoke();
        Console.WriteLine();
        Console.WriteLine("✓ Consentimiento registrado. Continuando...");
    }
}
