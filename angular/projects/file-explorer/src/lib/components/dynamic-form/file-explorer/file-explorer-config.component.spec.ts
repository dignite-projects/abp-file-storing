import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';

import { FileExplorerConfigComponent } from './file-explorer-config.component';
import { FileExplorerModule } from '../../../file-explorer.module';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileExplorerConfigComponent', () => {
  let component: FileExplorerConfigComponent;
  let fixture: ComponentFixture<FileExplorerConfigComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileExplorerModule, CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileExplorerConfigComponent);
    component = fixture.componentInstance;
    component.Entity = new FormGroup({
      formConfiguration: new FormGroup({
        'FileExplorer.FileContainerName': new FormControl(),
        'FileExplorer.UploadFileMultiple': new FormControl(),
      }),
    });
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
