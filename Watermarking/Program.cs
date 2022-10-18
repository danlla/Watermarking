using Microsoft.Data.Sqlite;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using System;
using System.IO;

namespace Watermarking
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //var path = "C:\\Users\\user\\source\\Watermarking\\828.mvt";

            //var path = "C:\\Users\\user\\source\\Watermarking\\stp.mbtiles";
            var path = "C:\\Users\\user\\source\\Watermarking\\isogd5.mvt";
            var path2 = "C:\\Users\\user\\source\\Watermarking\\mvttmp(5).pbf";

            //var z = 11;
            //var x = 359;
            //var y = 828;

            //var z = 7;
            //var x = 80;
            //var y = 40;

            //iosgd
            //var z = 9;
            //var x = 166;
            //var y = 325;

            //iosgd2
            //var z = 10;
            //var x = 657;
            //var y = 334;

            //iosgd3
            //var z = 10;
            //var x = 652;
            //var y = 333;

            //iosgd4
            //var z = 10;
            //var x = 656;
            //var y = 334;

            //iosgd5
            var z = 10;
            var x = 653;
            var y = 333;

            //iosgd6
            //var z = 12;
            //var x = 2619;
            //var y = 1333;

            //using var sqliteConnection = new SqliteConnection($"Data Source = {path}");
            //sqliteConnection.Open();

            //using var command = new SqliteCommand(@"SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y", sqliteConnection);
            //command.Parameters.AddWithValue("$z", z);
            //command.Parameters.AddWithValue("$x", x);
            //command.Parameters.AddWithValue("$y", (1 << z) - y - 1);
            //var tile = (byte[])command.ExecuteScalar();

            int extent = 4096;

            int size = 8;

            int key = 0;

            int countpoints = 15;

            var watermark = new Watermark(path, x, y, z, key, size, countPoints: countpoints, extent: extent);

            var count = 0;
            foreach (var layer in watermark.Tile.Layers)
                foreach (var feature in layer.Features)
                    foreach (var point in feature.Geometry.Coordinates)
                        count++;

            Console.WriteLine(count);
            Console.WriteLine(watermark.CountOutside());

            var bytes = new byte[] { 0x33 };

            var t2 = 0.4;
            var delta2 = 0.35;

            watermark.Embed(bytes, t2, delta2);

            var tileWatermarked = watermark.Tile;

            //tileWatermarked.Layers.RemoveAt(2);
            //tileWatermarked.Layers.RemoveAt(1);
            //tileWatermarked.Layers.RemoveAt(0);

            var w = new Watermark(tileWatermarked, x, y, z, key, size, countPoints: countpoints, extent: extent, m: watermark.M);

            count = 0;
            foreach (var layer in w.Tile.Layers)
                foreach (var feature in layer.Features)
                    foreach (var point in feature.Geometry.Coordinates)
                        count++;

            Console.WriteLine(count);
            Console.WriteLine(watermark.CountOutside());

            var resBytes = w.CheckWatermark(t2);

            for (var i = 0; i < size / 8; i++)
                Console.WriteLine($"{resBytes[i]:x}");

            using (var fileStream = new FileStream(path2, FileMode.Create))
                tileWatermarked.Write(fileStream, extent: (uint)extent);

            var ww = new Watermark(path2, x, y, z, key, size, countPoints: countpoints, extent: extent, m: watermark.M);

            count = 0;
            foreach (var layer in ww.Tile.Layers)
                foreach (var feature in layer.Features)
                    foreach (var point in feature.Geometry.Coordinates)
                        count++;

            Console.WriteLine(count);
            Console.WriteLine(watermark.CountOutside());

            var res = ww.CheckWatermark(t2);
            for (var i = 0; i < size / 8; i++)
                Console.WriteLine($"{res[i]:x}");
        }
    }
}
