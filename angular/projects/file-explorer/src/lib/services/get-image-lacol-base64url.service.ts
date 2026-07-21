import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class GetImageLacolBase64urlService {
  get(file: File) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.readAsDataURL(file);
      reader.onload = (event: any) => {
        resolve(event.target.result);
      };
      reader.onerror = error => reject(error);
    });
  }
}
