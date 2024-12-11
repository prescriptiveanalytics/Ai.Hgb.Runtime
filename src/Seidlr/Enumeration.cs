using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ai.Hgb.Runtime {
  public abstract class Enumeration : IComparable {
    public string Name { get; private set; }

    public int Id { get; private set; }

    protected Enumeration(int id, string name) => (Id, Name) = (id, name);

    public override string ToString() => Name;

    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public |
                            BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
                 .Select(f => f.GetValue(null))
                 .Cast<T>();

    public override bool Equals(object obj) {
      if (obj is not Enumeration otherValue) {
        return false;
      }

      var typeMatches = GetType().Equals(obj.GetType());
      var valueMatches = Id.Equals(otherValue.Id);

      return typeMatches && valueMatches;
    }

    public static bool operator ==(Enumeration left, Enumeration right) {
      return left.Equals(right);
    }

    public static bool operator !=(Enumeration left, Enumeration right) {
      return !left.Equals(right);
    }

    public int CompareTo(object other) => Id.CompareTo(((Enumeration)other).Id);

    public bool NameEquals(object obj) {      
      if (obj is not string otherValue) {
        return false;
      }
      return Name.Equals(otherValue);
    }

  }
}
