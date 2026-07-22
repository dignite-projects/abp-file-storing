import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class GetImageLacolBase64urlService {
  get(file: Blob): string {
    return URL.createObjectURL(file);
  }
}
