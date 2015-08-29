using IORAMHelper;
using System;
using System.Drawing;

namespace BMPLoaderNew
{
	/// <summary>
	/// Definiert einen Ladealgorithmus für unkomprimierte Bitmaps. Unterstützt werden die Bitzahlen 8 und 24, wobei immer auf 8-Bit reduziert wird. Ausgegeben werden nur Bottom-Up-Bitmaps.
	/// </summary>
	public class BMPLoader
	{
		/// <summary>
		/// Der Puffer für zu lesende bzw. zu schreibende Daten.
		/// </summary>
		private RAMBuffer _buffer;

		/// <summary>
		/// Der Bitmap-Header.
		/// </summary>
		private Header _header;

		/// <summary>
		/// Die Bitmap-Farbtabelle.
		/// </summary>
		private ColorTable _colorTable;

		/// <summary>
		/// Die Bilddaten in ihrer schlussendlich binär geschriebenen Form (d.h. mit Füllbytes).
		/// </summary>
		private byte[] _imageDataBin;

		/// <summary>
		/// Die Bilddaten in binärer Form, allerdings ohne Füllbytes (immer Top-Down).
		/// </summary>
		private byte[] _imageData;

		/// <summary>
		/// Erstellt ein neues Bitmap mit den angegebenen Abmessungen. Diese Bitmaps werden am Ende immer nach dem Bottom-Up-Verfahren geschrieben.
		/// </summary>
		/// <param name="width">Die Breite des zu erstellenden Bitmaps.</param>
		/// <param name="height">Die Höhe des zu erstellenden Bitmaps.</param>
		/// <param name="pal">Optional. Gibt die zu verwendende 256er-Farbtabelle an. Standardwert ist die 50500er-Farbtabelle.</param>
		public BMPLoader(int width, int height, JASCPalette pal = null)
		{
			// Header initialisieren
			_header = new Header();
			_header.height = Math.Abs(height);
			_header.width = Math.Abs(width);

			// Farbtabelle initialisieren
			if(pal == null)
			{
				// Standard-Farbpalettenreader abrufen
				JASCPalette tempPal;
				if(pal == null)
					tempPal = new JASCPalette(new RAMBuffer(BMPLoaderNew.Properties.Resources.pal50500));
				else
					tempPal = pal;

				// Farbpaletteninhalt in eigene Farbtabelle schreiben
				_colorTable = new ColorTable();
				for(int i = 0; i < tempPal._farben.GetLength(0); i++)
				{
					// Eintrag in Tabelle einfügen
					_colorTable[i] = Color.FromArgb(tempPal._farben[i, 0], tempPal._farben[i, 1], tempPal._farben[i, 2]);

					// Sicherheitshalber bei i = 255 abbrechen (falls Palette zu groß sein sollte)
					if(i == 255)
						break;
				}
			}
			else
			{
				// Benutzerdefinierten Farbpaletteninhalt in eigene Farbtabelle schreiben
				_colorTable = new ColorTable();
				for(int i = 0; i < pal._farben.GetLength(0); i++)
				{
					// Eintrag in Tabelle einfügen
					_colorTable[i] = Color.FromArgb(pal._farben[i, 0], pal._farben[i, 1], pal._farben[i, 2]);

					// Sicherheitshalber bei i = 255 abbrechen (falls Palette zu groß sein sollte)
					if(i == 255)
						break;
				}
			}

			// Bilddaten-Array initialisieren
			_imageData = new byte[_header.width * _header.height];
		}

		/// <summary>
		/// Lädt die angegebene Bitmap-Datei.
		/// </summary>
		/// <param name="filename">Der Pfad zur zu ladenden Bitmap-Datei.</param>
		/// <param name="pal">Optional. Gibt die zu verwendende 256er-Farbtabelle an. Sonst wird die entweder die im Bitmap angegebene oder die 50500er-Farbtabelle verwendet.</param>
		public BMPLoader(string filename, JASCPalette pal = null)
		{
			// Datei laden
			_buffer = new RAMBuffer(filename);

			// Header laden
			_header = new Header();
			_header.type = ReadUShort();
			_header.fileSize = ReadUInteger();
			_header.reserved = ReadUInteger();
			_header.offsetData = ReadUInteger();
			_header.imageHeaderSize = ReadUInteger();
			_header.width = ReadInteger();
			_header.height = ReadInteger();
			_header.layerCount = ReadUShort();
			_header.bitsPerPixel = ReadUShort();
			_header.compression = ReadUInteger();
			_header.size = ReadUInteger();
			_header.xDPI = ReadInteger();
			_header.yDPI = ReadInteger();
			_header.colorCount = ReadUInteger();
			_header.colorImportantCount = ReadUInteger();

			// Farbtabellenanzahl nachjustieren
			if(_header.colorCount == 0 && _header.bitsPerPixel == 8)
				_header.colorCount = 256;

			// Farbtabelle laden
			bool needAdjustColorTable = false;
			if(_header.colorCount > 0)
			{
				// Bildfarbtabelle laden
				_colorTable = new ColorTable(ref _buffer, _header.colorCount);

				// Falls eine Palette übergeben wurde, diese mit der Bildtabelle vergleichen
				if(pal == null || pal._farben.GetLength(0) != 256)
					needAdjustColorTable = true;
				else
					for(int i = 0; i < 256; ++i)
					{
						// Farben vergleichen
						Color aktF = _colorTable[i];
						if(pal._farben[i, 0] != aktF.R || pal._farben[i, 1] != aktF.G || pal._farben[i, 2] != aktF.B)
						{
							// Farbtabellen unterscheiden sich
							needAdjustColorTable = true;
							break;
						}
					}
			}
			else
			{
				// Bei 24-Bit-Bitmaps wird die Farbtabelle später geladen
				_colorTable = null;
			}

			// Nach Bitzahl unterscheiden
			if(_header.bitsPerPixel == 8)
			{
				// Bilddatenbreite ggf. auf ein Vielfaches von 4 Bytes erhöhen
				int width = _header.width; // Hilfsvariable zur Performanceerhöhung (immer gleichwertig mit _header.width)
				int width2 = width;
				while(width2 % 4 != 0)
				{
					width2++;
				}

				// Binäre Original-Bilddaten einlesen
				_imageDataBin = _buffer.ReadByteArray(width2 * Math.Abs(_header.height));

				// Neues Bilddaten-Array anlegen (ohne Füllbytes)
				_imageData = new byte[width * Math.Abs(_header.height)];

				// Richtung bestimmen
				bool dirTopDown = (_header.height < 0);

				// Der bisher nächste Farbindex
				byte nearestIndex = 0;

				// Der Abstand zum bisher nächsten Farbindex
				double nearestDistance;

				// Der aktuelle Farbabstand
				double tempDistance = 0.0;

				// Bilddaten abrufen
				int height2 = Math.Abs(_header.height);
				for(int x = 0; x < width2; x++)
				{
					for(int y = 0; y < height2; y++)
					{
						// Wenn es sich bei dem aktuellen Pixel um kein Füllbyte handelt, diesen übernehmen
						if(x < width)
						{
							// Pixel abrufen
							byte aktCol = _imageDataBin[y * width2 + x];

							// TODO: 0-Indizes in 255 umwandeln??

							// Falls nötig, Farben vergleichen
							if(needAdjustColorTable)
							{
								// Alle Farbwerte abrufen
								byte aktB = _colorTable[aktCol].B;
								byte aktG = _colorTable[aktCol].G;
								byte aktR = _colorTable[aktCol].R;

								// Die zur Pixelfarbe nächste Palettenfarbe suchen
								{
									// Werte zurücksetzen
									nearestIndex = 0;
									nearestDistance = 441.673; // Anfangswert: maximaler möglicher Abstand

									// Alle Einträge durchgehen
									for(int i = 0; i < 256; i++)
									{
										// Aktuelle Paletten-RGB-Werte abrufen
										byte pR = pal._farben[i, 0];
										byte pG = pal._farben[i, 1];
										byte pB = pal._farben[i, 2];

										// Gleiche Einträge sofort filtern
										if(aktR == pR && aktB == pB && aktG == pG)
										{
											// Paletten-Index überschreiben
											nearestIndex = (byte)i;

											// Fertig
											break;
										}

										// Abstand berechnen (Vektorlänge im dreidimensionalen RGB-Farbraum)
										tempDistance = Math.Sqrt(Math.Pow(aktR - pR, 2) + Math.Pow(aktG - pG, 2) + Math.Pow(aktB - pB, 2));

										// Vergleichen
										if(tempDistance < nearestDistance)
										{
											// Index merken
											nearestDistance = tempDistance;
											nearestIndex = (byte)i;
										}
									}

									// Paletten-Index überschreiben
									aktCol = nearestIndex;
								}
							} // Ende Adjust-ColorTable

							// Pixel zum Hauptbildarray hinzufügen und dabei nach Top-Down / Bottom-Up unterscheiden
							_imageData[(dirTopDown ? y : height2 - y - 1) * width + x] = aktCol;
						}
					}
				}
			}
			else if(_header.bitsPerPixel == 24)
			{
				// Es handelt sich um ein 24-Bit-Bitmap, somit muss eine Farbtabelle eingeführt werden
				{
					// Farbpalettenreader abrufen
					JASCPalette tempPal;
					if(pal == null)
						tempPal = new JASCPalette(new RAMBuffer(BMPLoaderNew.Properties.Resources.pal50500));
					else
						tempPal = pal;

					// Farbpaletteninhalt in eigene Farbtabelle schreiben
					_colorTable = new ColorTable();
					for(int i = 0; i < tempPal._farben.GetLength(0); i++)
					{
						// Eintrag in Tabelle einfügen
						_colorTable[i] = Color.FromArgb(tempPal._farben[i, 0], tempPal._farben[i, 1], tempPal._farben[i, 2]);

						// Sicherheitshalber bei i = 255 abbrechen (falls Palette zu groß sein sollte)
						if(i == 255)
							break;
					}
				}

				// Bilddatenbreite ggf. auf ein Vielfaches von 4 Bytes erhöhen
				int width = _header.width; // Hilfsvariable zur Performanceerhöhung (immer gleichwertig mit _header.width)
				int fillBytes = 0;
				while(((width * 3) + fillBytes) % 4 != 0)
				{
					fillBytes++;
				}

				// Binäre Original-Bilddaten einlesen
				_imageDataBin = _buffer.ReadByteArray((3 * width + fillBytes) * Math.Abs(_header.height));

				// Neues Bilddaten-Array anlegen (ohne Füllbytes)
				_imageData = new byte[width * Math.Abs(_header.height)];

				// Richtung bestimmen
				bool dirTopDown = (_header.height < 0);

				// Der bisher nächste Farbindex
				byte nearestIndex = 0;

				// Der Abstand zum bisher nächsten Farbindex
				double nearestDistance;

				// Der aktuelle Farbabstand
				double tempDistance = 0.0;

				// Bilddaten abrufen
				int height2 = Math.Abs(_header.height);
				for(int x = 0; x < width; x++)
				{
					for(int y = 0; y < height2; y++)
					{
						// Pixel abrufen
						byte aktB = _imageDataBin[y * (3 * width + fillBytes) + 3 * x];
						byte aktG = _imageDataBin[y * (3 * width + fillBytes) + 3 * x + 1];
						byte aktR = _imageDataBin[y * (3 * width + fillBytes) + 3 * x + 2];

						// Die zur Pixelfarbe nächste Palettenfarbe suchen
						{
							// Werte zurücksetzen
							nearestIndex = 0;
							nearestDistance = 441.673; // Anfangswert: maximaler möglicher Abstand

							// Alle Einträge durchgehen
							for(int i = 0; i < 256; i++)
							{
								// Aktuelle Paletten-RGB-Werte abrufen
								byte pR = _colorTable[i].R;
								byte pG = _colorTable[i].G;
								byte pB = _colorTable[i].B;

								// Gleiche Einträge sofort filtern
								if(aktR == pR && aktB == pB && aktG == pG)
								{
									// Pixel zum Hauptbildarray hinzufügen und dabei nach Top-Down / Bottom-Up unterscheiden
									_imageData[(dirTopDown ? y : height2 - y - 1) * width + x] = (byte)i;

									// Fertig
									break;
								}

								// Abstand berechnen (Vektorlänge im dreidimensionalen RGB-Farbraum)
								tempDistance = Math.Sqrt(Math.Pow(aktR - pR, 2) + Math.Pow(aktG - pG, 2) + Math.Pow(aktB - pB, 2));

								// Vergleichen
								if(tempDistance < nearestDistance)
								{
									// Index merken
									nearestDistance = tempDistance;
									nearestIndex = (byte)i;
								}
							}

							// Pixel zum Hauptbildarray hinzufügen und dabei nach Top-Down / Bottom-Up unterscheiden
							_imageData[(dirTopDown ? y : height2 - y - 1) * width + x] = nearestIndex;
						}
					}

					// Ggf. Füllbytes überspringen (bei Dateiende nicht)
					if(_buffer.Position < _buffer.Length - fillBytes)
						_buffer.Position = (_buffer.Position + fillBytes);
				}
			}
		}

		/// <summary>
		/// Speichert die enthaltene Bitmap in die angegebene Datei.
		/// </summary>
		/// <param name="filename">Die Datei, in die das Bild gespeichert werden soll.</param>
		public void saveToFile(string filename)
		{
			// Bilddatenbreite ggf. auf ein Vielfaches von 4 Bytes erhöhen
			int width = _header.width; // Hilfsvariable zur Performanceerhöhung (immer gleichwertig mit _header.width)
			int width2 = width;
			while(width2 % 4 != 0)
			{
				width2++;
			}

			// Bilddaten-Binär-Zielarray erstellen
			_imageDataBin = new byte[width2 * Math.Abs(_header.height)];

			// Bilddaten in Zielarray schreiben
			int height2 = Math.Abs(_header.height);
			for(int x = 0; x < width2; x++) // Start: Links
			{
				for(int y = 0; y < height2; y++) // Start: Oben
				{
					if(x >= width)
					{
						// Falls x außerhalb der Bildbreite liegt, Füllbyte einsetzen
						_imageDataBin[y * width2 + x] = 0;
					}
					else
					{
						// Normaler Pixel: Farbtabellenindex schreiben, dabei Bottom-Up-Richtung beachten
						_imageDataBin[y * width2 + x] = _imageData[(height2 - y - 1) * width + x];
					}
				}
			}

			// Header vorbereiten (einige wurden zwar schon definiert, aber lieber alle beisammen)
			_header.type = 19778;
			_header.fileSize = (uint)(44 + 256 * 4 + _imageDataBin.Length);
			_header.reserved = 0;
			_header.offsetData = (uint)(44 + 256 * 4);
			_header.imageHeaderSize = 40;
			_header.width = width;
			_header.height = height2;
			_header.layerCount = 1;
			_header.bitsPerPixel = 8;
			_header.compression = Header.COMPR_RGB;
			_header.size = (uint)(height2 * width);
			_header.xDPI = 0;
			_header.yDPI = 0;
			_header.colorCount = 0;
			_header.colorImportantCount = 0;

			// Puffer-Objekt erstellen
			_buffer = new RAMBuffer();

			// Header schreiben
			WriteUShort(_header.type);
			WriteUInteger(_header.fileSize);
			WriteUInteger(_header.reserved);
			WriteUInteger(_header.offsetData);
			WriteUInteger(_header.imageHeaderSize);
			WriteInteger(_header.width);
			WriteInteger(_header.height);
			WriteUShort(_header.layerCount);
			WriteUShort(_header.bitsPerPixel);
			WriteUInteger(_header.compression);
			WriteUInteger(_header.size);
			WriteInteger(_header.xDPI);
			WriteInteger(_header.yDPI);
			WriteUInteger(_header.colorCount);
			WriteUInteger(_header.colorImportantCount);

			// Farbtabelle schreiben
			_colorTable.ToBinary(ref _buffer);

			// Bilddaten schreiben
			WriteBytes(_imageDataBin);

			// Bitmap schreiben
			_buffer.Save(filename);
		}

		/// <summary>
		/// Gibt den Farbpalettenindex an der Position (x, y) zurück oder legt diesen fest.
		/// </summary>
		/// <param name="x">Die X-Koordinate des betreffenden Pixels.</param>
		/// <param name="y">Die Y-Koordinate des betreffenden Pixels.</param>
		/// <returns></returns>
		public byte this[int x, int y]
		{
			get
			{
				// Sicherheitsüberprüfung
				if(x >= _header.width || y >= Math.Abs(_header.height))
				{
					// Fehler
					throw new ArgumentOutOfRangeException("Die angegebene Position liegt nicht innerhalb des Bildes!");
				}

				// Wert zurückgeben
				return _imageData[y * _header.width + x];
			}
			set
			{
				// Sicherheitsüberprüfung
				if(x >= _header.width || y >= Math.Abs(_header.height))
				{
					// Fehler
					throw new ArgumentOutOfRangeException("Die angegebene Position liegt nicht innerhalb des Bildes!");
				}

				// Farbwert zuweisen
				_imageData[y * _header.width + x] = value;
			}
		}

		/// <summary>
		/// Gibt den Farbpalettenindex an der angegebenen Position zurück oder legt diesen fest.
		/// </summary>
		/// <param name="pos">Die Position des betreffenden Pixels.</param>
		/// <returns></returns>
		public byte this[Point pos]
		{
			get
			{
				// Wert zurückgeben
				return this[pos.X, pos.Y];
			}
			set
			{
				// Wert zuweisen
				this[pos.X, pos.Y] = value;
			}
		}

		/// <summary>
		/// Ruft die Bildbreite ab.
		/// </summary>
		public int Width
		{
			get
			{
				// Breite zurückgeben
				return _header.width;
			}
		}

		/// <summary>
		/// Ruft die Bildhöhe ab.
		/// </summary>
		public int Height
		{
			get
			{
				// Breite zurückgeben
				return Math.Abs(_header.height);
			}
		}

		#region Hilfsfunktionen

		// Die folgenden Funktionen sind Abkürzungen, in C++ wären dies Makros.

		#region Lesen

		/// <summary>
		/// Gibt genau ein Byte aus DataBuffer zurück.
		/// </summary>
		/// <returns></returns>
		/// <remarks></remarks>
		private byte ReadByte()
		{
			return _buffer.ReadByte();
		}

		/// <summary>
		/// Gibt ein Byte-Array aus DataBuffer zurück.
		/// </summary>
		/// <param name="Anzahl">Die Anzahl der auszulesenden Bytes.</param>
		/// <returns></returns>
		/// <remarks></remarks>
		private byte[] ReadBytes(uint Anzahl)
		{
			return _buffer.ReadByteArray((int)Anzahl);
		}

		/// <summary>
		/// Gibt genau einen UShort-Wert aus DataBuffer zurück.
		/// </summary>
		/// <returns></returns>
		/// <remarks></remarks>
		private ushort ReadUShort()
		{
			return _buffer.ReadUShort();
		}

		/// <summary>
		/// Gibt genau einen Integer-Wert aus DataBuffer zurück.
		/// </summary>
		/// <returns></returns>
		/// <remarks></remarks>
		private int ReadInteger()
		{
			return _buffer.ReadInteger();
		}

		/// <summary>
		/// Gibt genau einen UInteger-Wert aus DataBuffer zurück.
		/// </summary>
		/// <returns></returns>
		/// <remarks></remarks>
		private uint ReadUInteger()
		{
			return _buffer.ReadUInteger();
		}

		#endregion Lesen

		#region Schreiben

		/// <summary>
		/// Schreibt ein Byte-Array an das Ende des Puffers.
		/// </summary>
		/// <param name="Wert">Das zu schreibende Byte-Array.</param>
		/// <remarks></remarks>
		private void WriteBytes(byte[] Wert)
		{
			_buffer.Write(Wert);
		}

		/// <summary>
		/// Schreibt einen UShort-Wert an das Ende des Puffers.
		/// </summary>
		/// <param name="Wert">Der zu schreibende Wert.</param>
		/// <remarks></remarks>
		private void WriteUShort(ushort Wert)
		{
			_buffer.WriteUShort(Wert);
		}

		/// <summary>
		/// Schreibt einen Integer-Wert an das Ende des Puffers.
		/// </summary>
		/// <param name="Wert">Der zu schreibende Wert.</param>
		/// <remarks></remarks>
		private void WriteInteger(int Wert)
		{
			_buffer.WriteInteger(Wert);
		}

		/// <summary>
		/// Schreibt einen UInteger-Wert an das Ende des Puffers.
		/// </summary>
		/// <param name="Wert">Der zu schreibende Wert.</param>
		/// <remarks></remarks>
		private void WriteUInteger(uint Wert)
		{
			_buffer.WriteUInteger(Wert);
		}

		#endregion Schreiben

		#endregion Hilfsfunktionen

		#region Strukturen

		/// <summary>
		/// Definiert den Bitmap-Header.
		/// </summary>
		private struct Header
		{
			/// <summary>
			/// Definiert eine unkomprimierte Bitmap-Datei.
			/// </summary>
			internal const uint COMPR_RGB = 0;

			/// <summary>
			/// Definiert den Dateityp. Immer 19778 ("BM").
			/// </summary>
			internal ushort type;

			/// <summary>
			/// Die Größe der Bitmap-Datei.
			/// </summary>
			internal uint fileSize;

			/// <summary>
			/// 4 reservierte Bytes.
			/// </summary>
			internal uint reserved;

			/// <summary>
			/// Das Offset der Pixeldaten.
			/// </summary>
			internal uint offsetData;

			/// <summary>
			/// Die Länge des Bildheaders. Immer 40.
			/// </summary>
			internal uint imageHeaderSize;

			/// <summary>
			/// Die Breite des Bilds.
			/// </summary>
			internal int width;

			/// <summary>
			/// Die Höhe des Bilds.
			/// Vorsicht: Wenn die Höhe positiv ist, wurde das Bild von unten nach oben geschrieben, bei negativer Höhe von oben nach unten.
			/// </summary>
			internal int height;

			/// <summary>
			/// Die Anzahl der Farbebenen. Immer 1.
			/// </summary>
			internal ushort layerCount;

			/// <summary>
			/// Die Anzahl der Bits pro Pixel.
			/// </summary>
			internal ushort bitsPerPixel;

			/// <summary>
			/// Die verwendete Bildkompression.
			/// </summary>
			internal uint compression;

			/// <summary>
			/// Die Größe der Bilddaten.
			/// </summary>
			internal uint size;

			/// <summary>
			/// Die horizontale Auflösung des Zielausgabegeräts in Pixeln pro Meter. Meist 0.
			/// </summary>
			internal int xDPI;

			/// <summary>
			/// Die vertikale Auflösung des Zielausgabegeräts in Pixeln pro Meter. Meist 0.
			/// </summary>
			internal int yDPI;

			/// <summary>
			/// Die Anzahl der Farben in der Farbtabelle. Meist 0 (bedeutet Maximalzahl, d.h. 2 ^ bitsPerPixel).
			/// </summary>
			internal uint colorCount;

			/// <summary>
			/// Die Anzahl der tatsächlich im Bild verwendeten Farben. Meist 0.
			/// </summary>
			internal uint colorImportantCount;
		}

		#endregion Strukturen
	}
}