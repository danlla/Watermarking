using System;
using System.IO;
using NetTopologySuite.IO.VectorTiles.Mapbox;
using NetTopologySuite.IO.VectorTiles;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.VectorTiles.Tiles;

namespace watermarking
{
    internal class Program
    {

        static int CountIncludes(VectorTile tile, Polygon polygon)
        {
            var includes = 0;
            foreach (var layer in tile.Layers)
            {
                foreach (var feature in layer.Features)
                {
                    foreach (var point in feature.Geometry.Coordinates)
                    {
                        var pointMeters = CoordinateConverter.DegreesToMeters(point);
                        if (polygon.Intersects(new Point(pointMeters)))
                            includes++;
                    }
                }
            }
            return includes;
        }

        static bool IsLessThanHalfEmptySquare(VectorTile tile, Envelope envelope, double m, int i)
        {
            var countSquare = 0;
            var countEmptySqure = 0;
            for (var j = 0; j < i; j++)
            {
                for (var k = 0; k < i; k++)
                {
                    var mEnv = new Envelope(new Coordinate(envelope.MinX + m * j, envelope.MinY + m * k), new Coordinate(envelope.MinX + m * (j + 1), envelope.MinY + m * (k + 1)));
                    var p = new Polygon(
                        new LinearRing(
                            new Coordinate[]
                            {
                                    new(mEnv.MinX, mEnv.MinY),
                                    new(mEnv.MinX, mEnv.MaxY),
                                    new(mEnv.MaxX, mEnv.MaxY),
                                    new(mEnv.MaxX, mEnv.MinY),
                                    new(mEnv.MinX, mEnv.MinY)
                            }
                    )
                    );

                    if (CountIncludes(tile, p) < 3)
                        countEmptySqure++;
                    countSquare++;
                }
            }
            Console.WriteLine($"count square: {countSquare}, count empty square: {countEmptySqure}, {(double)countEmptySqure / countSquare}");
            if ((double)countEmptySqure / countSquare < 0.5)
                return true;
            return false;

        }

        static int[,] GenerateWinx(int m, int n)
        {
            var r = (int)Math.Floor((double)m * m / n);
            Console.WriteLine($"r = {r}");
            Random random = new Random(0);
            int[,] winx = new int[m, m];

            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                    winx[i, j] = -1;


            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < r; j++)
                {
                    var x = 0;
                    var y = 0;
                    do
                    {
                        x = random.Next() % m;
                        y = random.Next() % m;
                    } while (winx[x, y] != -1);

                    winx[x, y] = i;
                }
            }

            return winx;
        }
        static void Main(string[] args)
        {
            var path = "C:\\Users\\user\\source\\Watermarking\\828.mvt";
            using var fileStream = new FileStream(path, FileMode.Open);

            var z = 11;
            var x = 359;
            var y = 828;
            var reader = new MapboxTileReader();
            var tile = reader.Read(fileStream, new NetTopologySuite.IO.VectorTiles.Tiles.Tile(x, y, z));

            var env = CoordinateConverter.TileBounds(x, y, z);
            Console.WriteLine(env);
            Console.WriteLine(env.Height);
            Console.WriteLine(env.Width);

            var envMeters = CoordinateConverter.DegreesToMeters(env);

            Console.WriteLine(envMeters);
            Console.WriteLine(envMeters.Height);
            Console.WriteLine(envMeters.Width);

            var a = 0.0;
            var m = 30;
            for (; m >= 2; m--)
            {
                a = envMeters.Height / m;
                Console.WriteLine(a);

                if (IsLessThanHalfEmptySquare(tile, envMeters, a, m))
                    break;
            }

            Console.WriteLine($"a = {a}, m = {m}");

            var winx = GenerateWinx(m, 100);

            for (var i = 0; i < m; i++)
            {
                for (var j = 0; j < m; j++)
                {
                    Console.Write($"{winx[i, j]} ");
                }
                Console.WriteLine();
            }

            //Console.WriteLine(CoordinateConverter.MapSize(z));
            //var coor_min = CoordinateConverter.PositionToGlobalPixel(new double[] { env.MinX, env.MinY }, z);
            //var coor_max = CoordinateConverter.PositionToGlobalPixel(new double[] { env.MaxX, env.MaxY }, z);
            //Console.WriteLine($"{coor_min[0]}, {coor_min[1]}");
            //Console.WriteLine($"{coor_max[0]}, {coor_max[1]}");
            //Console.WriteLine($"{coor_max[0] - coor_min[0]}, {coor_max[1] - coor_min[1]}");
            //Console.WriteLine(tile.TileId);

            //foreach (var layer in tile.Layers)
            //{
            //    Console.WriteLine(layer.Name);
            //    foreach (var feature in layer.Features)
            //    {
            //        Console.WriteLine($"Geometry type:{feature.Geometry.GeometryType}");
            //        foreach (var point in feature.Geometry.Coordinates)
            //            Console.WriteLine($"({point.X}, {point.Y})\n");
            //    }
            //}
        }
    }
}
