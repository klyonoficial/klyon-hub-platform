var list = new[] { new { Type = ""w"", Title = ""t"" } }.ToList();
object boxed = list;
var asDynamic = boxed as System.Collections.Generic.List<dynamic>;
var asEnumerable = boxed as System.Collections.IEnumerable;
Console.WriteLine($""as List<dynamic>: {asDynamic != null}"");
Console.WriteLine($""as IEnumerable: {asEnumerable != null}"");
var dyn = (dynamic)boxed;
try {
  int count = 0;
  foreach (var x in dyn) { count++; Console.WriteLine(x.Title); }
  Console.WriteLine($""foreach dynamic count: {count}"");
} catch (Exception ex) { Console.WriteLine(""foreach fail: "" + ex.Message); }
