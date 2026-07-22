import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FileModalComponent } from './file-modal.component';

describe('FileModalComponent', () => {
  let component: FileModalComponent;
  let fixture: ComponentFixture<FileModalComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [FileModalComponent]
    });
    fixture = TestBed.createComponent(FileModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should mark a failed upload without throwing', async () => {
    const file = { name: 'failed.txt', size: 1 } as any;
    const testComponent = Object.create(FileModalComponent.prototype) as FileModalComponent;
    (testComponent as any).sizeLimit = 1024;
    (testComponent as any).list = { get: () => undefined };
    (testComponent as any).uploadPictureStatusList = [file];
    (testComponent as any).uploadingFile = () => Promise.reject(new Error('upload failed'));

    await testComponent.getFileChange({ target: { files: [file] } });
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(file.status).toBe(2);
  });
});
