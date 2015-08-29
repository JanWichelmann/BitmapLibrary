using IORAMHelper;
using System.Drawing;
using System.Drawing.Imaging;

/// <summary>
/// Repräsentiert eine Farbpalette (JASC *.pal-Format)
/// </summary>
/// <remarks></remarks>
namespace BMPLoaderNew
{
	public class JASCPalette
	{
		private string _header = "";
		private string _version = "";
		private int _anzFarben = 0;

		/// <summary>
		/// Enthält die Farbdaten. Sortierung: FarbID =&gt; Array(0 =&gt; R, 1 =&gt; G, 2 =&gt; B)
		/// </summary>
		/// <remarks></remarks>
		public byte[,] _farben;

		/// <summary>
		/// Der Datenpuffer.
		/// </summary>
		private RAMBuffer _buffer;

		/// <summary>
		/// Erstellt eine neue Instanz von JASCPalette aus den angegebenen Daten.
		/// </summary>
		/// <param name="Data">Die Farbdaten als PufferKlasse-Objekt.</param>
		/// <remarks></remarks>
		public JASCPalette(RAMBuffer data)
		{
			_buffer = data;
			ReadPalette();
		}

		/// <summary>
		/// Liest die Farbpalette.
		/// </summary>
		/// <remarks></remarks>
		private void ReadPalette()
		{
			_buffer.Position = 0;

			// Header
			byte aktByte = _buffer.ReadByte();
			while(aktByte != 13 & aktByte != 10)
			{
				_header += (char)(aktByte);
				aktByte = _buffer.ReadByte();
			}

			// Übergehen von Zeilenumbruch (LF)
			_buffer.ReadByte();

			// Version
			aktByte = _buffer.ReadByte();
			while(aktByte != 13 & aktByte != 10)
			{
				_version += (char)(aktByte);
				aktByte = _buffer.ReadByte();
			}

			// Übergehen von Zeilenumbruch (LF)
			_buffer.ReadByte();

			// Anzahl Farben
			string AnzFarbenTemp = "";
			aktByte = _buffer.ReadByte();
			while(aktByte != 13 & aktByte != 10)
			{
				AnzFarbenTemp += (char)(aktByte);
				aktByte = _buffer.ReadByte();
			}
			_anzFarben = int.Parse(AnzFarbenTemp);

			// Übergehen von Zeilenumbruch (LF)
			_buffer.ReadByte();

			// Farbdaten Zeile für Zeile auslesen
			_farben = new byte[_anzFarben, 3];
			for(int i = 0; i < _anzFarben; i++)
			{
				// Rot
				string RTemp = "";
				aktByte = _buffer.ReadByte();
				while(aktByte != 13 & aktByte != 10 & aktByte != 32)
				{
					RTemp += (char)(aktByte);
					aktByte = _buffer.ReadByte();
				}

				// Grün
				string GTemp = "";
				aktByte = _buffer.ReadByte();
				while(aktByte != 13 & aktByte != 10 & aktByte != 32)
				{
					GTemp += (char)(aktByte);
					aktByte = _buffer.ReadByte();
				}

				// Blau
				string BTemp = "";
				aktByte = _buffer.ReadByte();
				while(aktByte != 13 & aktByte != 10 & aktByte != 32)
				{
					BTemp += (char)(aktByte);
					aktByte = _buffer.ReadByte();
				}

				// Bytewerte aus Strings erstellen
				byte R = byte.Parse(RTemp);
				byte G = byte.Parse(GTemp);
				byte B = byte.Parse(BTemp);

				// Werte in Farbarray einfügen
				_farben[i, 0] = R;
				_farben[i, 1] = G;
				_farben[i, 2] = B;

				// Übergehen von Zeilenumbruch (LF)
				_buffer.ReadByte();
			}
		}

		/// <summary>
		/// Erstellt eine .NET-Farbpalette aus der geladenen Palettendatei.
		/// </summary>
		/// <returns></returns>
		public ColorPalette GetColorPalette()
		{
			// Palettenobjekt intialisieren
			ColorPalette Palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;

			// Alle Farben einzeln übertragen
			for(int i = 0; i < _anzFarben; i++)
			{
				// ARGB-Werte bestimmen
				byte A = 0;
				byte R = _farben[i, 0];
				byte G = _farben[i, 1];
				byte B = _farben[i, 2];

				// Eintrag schreiben
				Palette.Entries[i] = Color.FromArgb(A, R, G, B);
			}

			// Fertig
			return Palette;
		}

		#region Eigenschaften

		/// <summary>
		/// Gibt die zur Farb-ID passende Farbe als Color-Objekt zurück.
		/// </summary>
		/// <param name="ColorID">Die ID der abzurufenden Farbe.</param>
		/// <param name="useWhite255Transp">Legt fest, ob Weiß 255 als transparent angegeben werden soll.</param>
		/// <returns></returns>
		/// <remarks></remarks>
		public Color GetColor(ushort ColorID, bool useWhite255Transp = false)
		{
			// Farbe zurückgeben
			return Color.FromArgb((useWhite255Transp && ColorID == 255) ? 0 : 255, _farben[ColorID, 0], _farben[ColorID, 1], _farben[ColorID, 2]);
		}

		#endregion Eigenschaften
	}
}