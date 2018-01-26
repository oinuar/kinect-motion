
namespace KinectMotion
{
   struct ServerMessage<T>
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
