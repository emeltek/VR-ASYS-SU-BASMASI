using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;

public class RainStart : MonoBehaviour
{
   // Inspector'dan atayacağın diğer script referansı
   public WaterRiseEffect WaterRiseEffect;

   void Update()
   {
      // Space tuşuna basıldığında (Sadece basıldığı ilk karede tetiklenir)
      if (Input.GetKeyDown(KeyCode.Space))
      {
         // Referansın boş olup olmadığını kontrol etmek hata almamanı sağlar
         if (WaterRiseEffect != null)
         {
            // Başlatmak istediğin metodu buraya yazıyorsun
            WaterRiseEffect.StartWaterSequence(); 
         }
         else
         {
            Debug.LogWarning("WaterRiseEffect referansı Inspector'da atanmamış!");
         }
      }
   }
}