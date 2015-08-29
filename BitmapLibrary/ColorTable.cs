using IORAMHelper;
using System;
using System.Drawing;

namespace BMPLoaderNew
{
	/// <summary>
	/// Definiert eine 8-Bit-Bitmap-Farbtabelle (256 Einträge). Der Alpha-Wert wird dabei grundsätzlich ignoriert.
	/// </summary>
	public class ColorTable
	{
		/// <summary>
		/// Die Farbtabelle selbst.
		/// </summary>
		private Color[] _colors = new Color[256];

		/// <summary>
		/// Erstellt eine neue leere Farbtabelle.
		/// </summary>
		public ColorTable()
		{
			// Nichts tun
		}

		/// <summary>
		/// Erstellt eine neue Farbtabelle aus einen angegebenen JASCPalette-Objekt. <param name="pal">Die zu ladende JASC-Palette.</param>
		/// </summary>
		public ColorTable(JASCPalette pal)
		{
			// Anzahl der Farben in pal verwenden, aber maximal 256
			int count = Math.Min(pal._farben.GetLength(0), 256);

			// Alle Farben der Palette durchlaufen
			for(int i = 0; i < count; i++)
			{
				// Farbeintrag erstellen
				_colors[i] = Color.FromArgb(pal._farben[i, 0], pal._farben[i, 1], pal._farben[i, 2]);
			}
		}

		/// <summary>
		/// Erstellt eine neue Farbtabelle aus derm Bitmap-Puffer (d.h. aus einer binären Bitmap-Tabelle).
		/// </summary>
		/// <param name="buffer">Der Bitmap-Puffer mit einem Zeiger am Beginn der Farbtabelle.</param>
		/// <param name="count">Die Anzahl der in der Tabelle definierten Farben.</param>
		public ColorTable(ref RAMBuffer buffer, uint count)
		{
			// Einträge einzeln einlesen
			for(int i = 0; i < count; ++i)
			{
				// 4 Bytes lesen
				byte[] b = buffer.ReadByteArray(4);

				// Farbeintrag erstellen
				_colors[i] = Color.FromArgb(b[2], b[1], b[0]);
			}

			// Fehlende Einträge mit 255er-Weiß initialisieren
			for(int i = (int)count; i < 256; ++i)
			{
				// Weiß einfügen
				_colors[i] = Color.FromArgb(255, 255, 255);
			}
		}

		/// <summary>
		/// Schreibt alle Farbdaten als Bitmap-Farbtabelle in den angegebenen Puffer.
		/// </summary>
		/// <param name="buffer">Der zu verwendende Puffer.</param>
		public void ToBinary(ref RAMBuffer buffer)
		{
			// Einträge einzeln durchgehen
			for(int i = 0; i < 256; i++)
			{
				// 4 Bytes nehmen (d.h. einen Tabelleneintrag)
				byte[] b = { _colors[i].B, _colors[i].G, _colors[i].R, 0 };

				// Bytes schreiben
				buffer.Write(b);
			}
		}

		/// <summary>
		/// Ruft die Farbe am angegebenen Index ab oder legt diese fest.
		/// </summary>
		/// <param name="index">Der Index der abzurufenden oder zu ändernden Farbe.</param>
		/// <returns></returns>
		public Color this[int index]
		{
			get
			{
				// Sicherheitsabfrage
				if(index > 255)
					throw new ArgumentOutOfRangeException("Der angegebene Farbpalettenindex ist nicht innerhalb der Palette!");

				// Eintrag zurückgeben
				return _colors[index];
			}
			set
			{
				// Sicherheitsabfrage
				if(index > 255)
					throw new ArgumentOutOfRangeException("Der angegebene Farbpalettenindex ist nicht innerhalb der Palette!");

				// Eintrag ändern (explizite Neudefinition, um etwaige Alpha-Werte herauszuschmeißen)
				_colors[index] = Color.FromArgb(value.R, value.G, value.B);
			}
		}
	}
}