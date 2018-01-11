
namespace KinectMotion
{
   struct Message<T>
   {
      public string Type
      {
         get
         {
            return typeof(T).Name;
         }
      }

      public T Content { get; set; }
   }
}
